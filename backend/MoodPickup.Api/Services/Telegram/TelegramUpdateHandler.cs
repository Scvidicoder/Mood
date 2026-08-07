using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MoodPickup.Api.Data;
using MoodPickup.Api.DTOs.Telegram;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Extensions;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Infrastructure.Telegram;
using MoodPickup.Api.Interfaces;
using MoodPickup.Api.Options;

namespace MoodPickup.Api.Services.Telegram;

public sealed partial class TelegramUpdateHandler(
    MoodPickupDbContext dbContext,
    ITelegramBotClient botClient,
    ITelegramOtpSender otpSender,
    TelegramMessageProvider messages,
    TelegramAbuseLimiter abuseLimiter,
    AuthenticationHashing hashing,
    IOptions<TelegramOptions> telegramOptions,
    IOptions<OtpOptions> otpOptions,
    TimeProvider timeProvider,
    ILogger<TelegramUpdateHandler> logger) : ITelegramUpdateHandler
{
    private readonly TelegramOptions _telegramOptions = telegramOptions.Value;
    private readonly OtpOptions _otpOptions = otpOptions.Value;

    public async Task HandleAsync(
        TelegramUpdateDto update,
        CancellationToken cancellationToken)
    {
        if (!await TryReserveUpdateAsync(update.UpdateId, cancellationToken))
        {
            logger.LogInformation(
                "Duplicate Telegram update {UpdateId} was acknowledged.",
                update.UpdateId);
            return;
        }

        logger.LogInformation(
            "Processing Telegram update {UpdateId} with supported type {UpdateType}.",
            update.UpdateId,
            update.Message is null ? "unsupported" : "message");

        try
        {
            await DispatchAsync(update.Message, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogInformation(
                "Telegram update {UpdateId} lost a safe challenge race and was acknowledged.",
                update.UpdateId);
        }
        catch (Exception exception)
        {
            try
            {
                dbContext.ChangeTracker.Clear();
                await ReleaseUpdateAsync(
                    update.UpdateId,
                    CancellationToken.None);
            }
            catch (Exception releaseException)
            {
                logger.LogWarning(
                    "Telegram update {UpdateId} failed and its idempotency reservation could not be released due to {FailureType}.",
                    update.UpdateId,
                    releaseException.GetType().Name);
            }

            logger.LogWarning(
                "Telegram update {UpdateId} failed due to {FailureType}; Telegram may retry it.",
                update.UpdateId,
                exception.GetType().Name);
            throw;
        }
    }

    private async Task DispatchAsync(
        TelegramMessageDto? message,
        CancellationToken cancellationToken)
    {
        if (message?.From is not { IsBot: false } sender ||
            !string.Equals(message.Chat.Type, "private", StringComparison.Ordinal) ||
            message.Chat.Id != sender.Id)
        {
            return;
        }

        if (message.Contact is not null)
        {
            await HandleContactAsync(message, sender, cancellationToken);
            return;
        }

        var command = ParseCommand(message.Text);
        if (command is null)
        {
            return;
        }

        if (command.Value.Name == "help")
        {
            await SendTextAsync(
                message.Chat.Id,
                messages.Help,
                cancellationToken: cancellationToken);
            return;
        }

        if (command.Value.Name != "start")
        {
            return;
        }

        await HandleStartAsync(
            message.Chat.Id,
            sender,
            command.Value.Argument,
            cancellationToken);
    }

    private async Task HandleStartAsync(
        long chatId,
        TelegramUserDto sender,
        string? startToken,
        CancellationToken cancellationToken)
    {
        var limit = abuseLimiter.ConsumeStartAttempt(sender.Id);
        if (limit == TelegramLimitResult.Blocked)
        {
            logger.LogWarning(
                "Telegram /start attempt was blocked for Telegram user {TelegramUserId}.",
                sender.Id);
            return;
        }
        if (limit == TelegramLimitResult.LimitReached)
        {
            await SendTextAsync(
                chatId,
                messages.TooManyAttempts,
                new TelegramReplyKeyboardRemove(),
                cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(startToken))
        {
            await SendTextAsync(
                chatId,
                messages.Welcome,
                cancellationToken: cancellationToken);
            return;
        }

        if (!StartTokenRegex().IsMatch(startToken))
        {
            await SendInvalidLinkAsync(chatId, cancellationToken);
            return;
        }

        var tokenHash = hashing.HashTelegramLinkToken(startToken);
        var now = timeProvider.GetUtcNow();
        var challenge = await dbContext.LoginChallenges
            .SingleOrDefaultAsync(
                candidate => candidate.TelegramLinkTokenHash == tokenHash,
                cancellationToken);
        if (!CanStart(challenge, sender.Id, now))
        {
            await SendInvalidLinkAsync(chatId, cancellationToken);
            return;
        }

        var otherChallenges = await dbContext.LoginChallenges
            .Where(candidate =>
                candidate.Id != challenge!.Id &&
                candidate.TelegramUserId == sender.Id &&
                !candidate.IsUsed &&
                candidate.TelegramLinkUsedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var otherChallenge in otherChallenges)
        {
            otherChallenge.TelegramLinkUsedAt = now;
            otherChallenge.IsUsed = true;
        }

        challenge!.TelegramUserId = sender.Id;
        challenge.TelegramUsername = NormalizeUsername(sender.Username);
        challenge.TelegramStartedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        var keyboard = new TelegramReplyKeyboardMarkup(
            [
                [
                    new TelegramKeyboardButton(
                        messages.ContactButton,
                        RequestContact: true)
                ]
            ]);
        await SendTextAsync(
            chatId,
            messages.ShareContact,
            keyboard,
            cancellationToken);

        logger.LogInformation(
            "Telegram linking started for challenge {ChallengeId}.",
            challenge.Id);
    }

    private async Task HandleContactAsync(
        TelegramMessageDto message,
        TelegramUserDto sender,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var challenge = await dbContext.LoginChallenges
            .Where(candidate =>
                candidate.TelegramUserId == sender.Id &&
                candidate.TelegramStartedAt != null &&
                candidate.TelegramLinkUsedAt == null &&
                !candidate.IsUsed)
            .OrderByDescending(candidate => candidate.TelegramStartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (challenge is null ||
            challenge.TelegramLinkExpiresAt is null ||
            challenge.TelegramLinkExpiresAt <= now ||
            challenge.ExpiresAt <= now)
        {
            await SendInvalidLinkAsync(message.Chat.Id, cancellationToken);
            return;
        }

        var contact = message.Contact!;
        if (contact.UserId is null ||
            contact.UserId != sender.Id ||
            !PhoneNumberNormalizer.TryNormalize(
                contact.PhoneNumber,
                out var contactPhone))
        {
            await RejectContactAsync(
                challenge,
                message.Chat.Id,
                null,
                cancellationToken);
            return;
        }

        if (!string.Equals(
                contactPhone,
                challenge.PhoneNumber,
                StringComparison.Ordinal))
        {
            await RejectContactAsync(
                challenge,
                message.Chat.Id,
                contactPhone,
                cancellationToken);
            return;
        }

        var customer = await dbContext.Customers
            .SingleOrDefaultAsync(
                candidate => candidate.PhoneNumber == challenge.PhoneNumber,
                cancellationToken);
        var customerId = customer?.Id;
        var identityUsedElsewhere = await dbContext.Customers
            .AnyAsync(
                candidate =>
                    candidate.TelegramChatId == message.Chat.Id &&
                    (customerId == null || candidate.Id != customerId),
                cancellationToken);
        var customerHasDifferentIdentity =
            customer?.TelegramChatId is long existingChatId &&
            existingChatId != message.Chat.Id;
        if (identityUsedElsewhere || customerHasDifferentIdentity)
        {
            LockChallenge(challenge, now);
            await dbContext.SaveChangesAsync(cancellationToken);
            await SendTextAsync(
                message.Chat.Id,
                messages.IdentityConflict,
                new TelegramReplyKeyboardRemove(),
                cancellationToken);
            logger.LogWarning(
                "Telegram identity conflict rejected for challenge {ChallengeId}.",
                challenge.Id);
            return;
        }

        var oneTimeCode =
            RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        challenge.TelegramChatId = message.Chat.Id;
        challenge.TelegramUserId = sender.Id;
        challenge.TelegramUsername = NormalizeUsername(sender.Username);
        challenge.TelegramLinkUsedAt = now;
        challenge.TelegramLinkedAt = now;
        challenge.TelegramContactVerifiedAt = now;
        challenge.CodeHash =
            hashing.HashOneTimeCode(challenge.Id, oneTimeCode);
        challenge.ExpiresAt = now.AddMinutes(_otpOptions.LifetimeMinutes);
        challenge.LastSentAt = now;
        challenge.TelegramDeliveryFailedAt = null;

        if (customer is not null && customer.TelegramChatId is null)
        {
            customer.TelegramChatId = message.Chat.Id;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await otpSender.SendAsync(
                challenge,
                oneTimeCode,
                cancellationToken);
            challenge.OtpSentAt = timeProvider.GetUtcNow();
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Telegram contact linked successfully for challenge {ChallengeId}.",
                challenge.Id);
        }
        catch (TelegramApiException exception)
        {
            challenge.CodeHash = null;
            challenge.TelegramDeliveryFailureCount++;
            challenge.TelegramDeliveryFailedAt = timeProvider.GetUtcNow();
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogWarning(
                "Telegram OTP delivery failed for challenge {ChallengeId}, method {MethodName}.",
                challenge.Id,
                exception.MethodName);
        }
    }

    private async Task RejectContactAsync(
        LoginChallenge challenge,
        long chatId,
        string? suppliedPhone,
        CancellationToken cancellationToken)
    {
        challenge.TelegramLinkAttemptCount++;
        var locked =
            challenge.TelegramLinkAttemptCount >=
            _telegramOptions.MaximumContactMismatchAttempts;
        if (locked)
        {
            LockChallenge(challenge, timeProvider.GetUtcNow());
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await SendTextAsync(
            chatId,
            locked ? messages.TooManyAttempts : messages.PhoneMismatch,
            locked ? new TelegramReplyKeyboardRemove() : null,
            cancellationToken);
        logger.LogWarning(
            "Telegram contact mismatch for challenge {ChallengeId}; supplied phone {MaskedPhone}; attempt {AttemptCount}.",
            challenge.Id,
            suppliedPhone is null
                ? "unverified"
                : PhoneNumberMasker.Mask(suppliedPhone),
            challenge.TelegramLinkAttemptCount);
    }

    private async Task SendInvalidLinkAsync(
        long chatId,
        CancellationToken cancellationToken)
    {
        await SendTextAsync(
            chatId,
            messages.InvalidOrExpiredLink,
            new TelegramReplyKeyboardRemove(),
            cancellationToken);
    }

    private async Task SendTextAsync(
        long chatId,
        string text,
        object? replyMarkup = null,
        CancellationToken cancellationToken = default)
    {
        await botClient.SendMessageAsync(
            new TelegramSendMessageRequest(chatId, text, replyMarkup),
            cancellationToken);
    }

    private async Task<bool> TryReserveUpdateAsync(
        long updateId,
        CancellationToken cancellationToken)
    {
        if (await dbContext.TelegramProcessedUpdates.AnyAsync(
                update => update.UpdateId == updateId,
                cancellationToken))
        {
            return false;
        }

        dbContext.TelegramProcessedUpdates.Add(new TelegramProcessedUpdate
        {
            UpdateId = updateId,
            ProcessedAt = timeProvider.GetUtcNow()
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    private async Task ReleaseUpdateAsync(
        long updateId,
        CancellationToken cancellationToken)
    {
        var update = await dbContext.TelegramProcessedUpdates
            .SingleOrDefaultAsync(
                candidate => candidate.UpdateId == updateId,
                cancellationToken);
        if (update is null)
        {
            return;
        }

        dbContext.TelegramProcessedUpdates.Remove(update);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private bool CanStart(
        LoginChallenge? challenge,
        long telegramUserId,
        DateTimeOffset now)
    {
        return challenge is
        {
            IsUsed: false,
            TelegramLinkUsedAt: null,
            TelegramLinkExpiresAt: not null
        } &&
        challenge.TelegramLinkExpiresAt > now &&
        challenge.ExpiresAt > now &&
        challenge.TelegramLinkAttemptCount <
        _telegramOptions.MaximumContactMismatchAttempts &&
        (challenge.TelegramUserId is null ||
         challenge.TelegramUserId == telegramUserId);
    }

    private static void LockChallenge(
        LoginChallenge challenge,
        DateTimeOffset now)
    {
        challenge.TelegramLinkUsedAt = now;
        challenge.TelegramDeliveryFailedAt ??= now;
        challenge.CodeHash = null;
    }

    private static (string Name, string? Argument)? ParseCommand(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || !text.StartsWith('/'))
        {
            return null;
        }

        var parts = text.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0][1..].Split('@', 2)[0].ToLowerInvariant();
        return (command, parts.Length == 2 ? parts[1].Trim() : null);
    }

    private static string? NormalizeUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        var normalized = username.Trim().TrimStart('@');
        return normalized.Length <= 64 ? normalized : normalized[..64];
    }

    [GeneratedRegex(@"^[A-Za-z0-9_-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex StartTokenRegex();
}
