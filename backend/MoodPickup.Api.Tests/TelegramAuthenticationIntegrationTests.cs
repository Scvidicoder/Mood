using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoodPickup.Api.Data;
using MoodPickup.Api.DTOs;
using MoodPickup.Api.DTOs.Telegram;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Infrastructure.Telegram;

namespace MoodPickup.Api.Tests;

public sealed class TelegramAuthenticationIntegrationTests(
    MoodPickupApiFactory factory) : IClassFixture<MoodPickupApiFactory>
{
    private readonly HttpClient _client = factory.CreateSecureClient();

    [Fact]
    public async Task Webhook_RequiresSecretAndSafelyHandlesInvalidPayloads()
    {
        await factory.ResetAuthenticationStateAsync();

        using var missing = await PostWebhookAsync(
            new { update_id = 1L },
            secret: null);
        using var incorrect = await PostWebhookAsync(
            new { update_id = 2L },
            "incorrect_secret");
        using var supportedSecret = await PostWebhookAsync(
            new { update_id = 3L },
            WebhookSecret);
        using var malformedRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/telegram/webhook")
        {
            Content = new StringContent(
                "{",
                Encoding.UTF8,
                "application/json")
        };
        malformedRequest.Headers.Add(
            TelegramWebhookSecretFilter.SecretHeaderName,
            WebhookSecret);
        using var malformed = await _client.SendAsync(malformedRequest);
        using var oversizedRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/telegram/webhook")
        {
            Content = new StringContent(
                new string('x', 70_000),
                Encoding.UTF8,
                "application/json")
        };
        oversizedRequest.Headers.Add(
            TelegramWebhookSecretFilter.SecretHeaderName,
            WebhookSecret);
        using var oversized = await _client.SendAsync(oversizedRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, incorrect.StatusCode);
        Assert.Equal(HttpStatusCode.OK, supportedSecret.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal(
            HttpStatusCode.RequestEntityTooLarge,
            oversized.StatusCode);
    }

    [Fact]
    public async Task RequestCode_ReturnsOpaqueDeepLinkAndProtectedStatus()
    {
        await factory.ResetAuthenticationStateAsync();
        factory.OtpSender.AllowsUnlinkedPhoneNumbers = false;

        var (response, responseJson) =
            await RequestLinkAsync("+992 900 000 020");
        var startToken = GetStartToken(response.TelegramBotUrl);

        Assert.Equal(
            CustomerChallengeStatus.WaitingForTelegramStart,
            response.Status);
        Assert.StartsWith(
            "https://t.me/test_bot?start=",
            response.TelegramBotUrl,
            StringComparison.Ordinal);
        Assert.Matches("^[A-Za-z0-9_-]{43}$", startToken);
        Assert.DoesNotContain("992900000020", startToken, StringComparison.Ordinal);
        Assert.DoesNotContain("otp", responseJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("codeHash", responseJson, StringComparison.OrdinalIgnoreCase);

        using var invalidStatus = await _client.PostAsJsonAsync(
            "/api/v1/auth/customer/challenge-status",
            new CustomerChallengeStatusRequest(
                response.ChallengeId,
                "wrong-client-secret"));
        using var validStatus = await _client.PostAsJsonAsync(
            "/api/v1/auth/customer/challenge-status",
            new CustomerChallengeStatusRequest(
                response.ChallengeId,
                response.ClientChallengeSecret));
        var status = await validStatus.Content
            .ReadFromJsonAsync<CustomerChallengeStatusResponse>();

        Assert.Equal(HttpStatusCode.Unauthorized, invalidStatus.StatusCode);
        Assert.Equal(HttpStatusCode.OK, validStatus.StatusCode);
        Assert.Equal(
            CustomerChallengeStatus.WaitingForTelegramStart,
            status!.Status);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext =
            scope.ServiceProvider.GetRequiredService<MoodPickupDbContext>();
        var challenge = await dbContext.LoginChallenges
            .SingleAsync(item => item.Id == response.ChallengeId);
        Assert.NotEqual(startToken, challenge.TelegramLinkTokenHash);
        Assert.NotEqual(
            response.ClientChallengeSecret,
            challenge.ClientStatusSecretHash);
        Assert.Null(challenge.CodeHash);
    }

    [Fact]
    public async Task StartAndMatchingContact_LinkRegisterAndProcessDuplicateOnce()
    {
        await factory.ResetAuthenticationStateAsync();
        factory.OtpSender.AllowsUnlinkedPhoneNumbers = false;
        const string phoneNumber = "+992900000021";
        const long telegramUserId = 7_000_000_021;

        var (challenge, _) = await RequestLinkAsync(phoneNumber);
        var startToken = GetStartToken(challenge.TelegramBotUrl);
        using var startResponse = await PostWebhookAsync(
            MessageUpdate(
                1001,
                telegramUserId,
                "private",
                $"/start {startToken}"),
            WebhookSecret);

        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        var contactRequest = Assert.Single(factory.BotClient.Messages);
        var keyboard = Assert.IsType<TelegramReplyKeyboardMarkup>(
            contactRequest.ReplyMarkup);
        Assert.True(keyboard.Keyboard.Single().Single().RequestContact);

        var contactUpdate = ContactUpdate(
            1002,
            telegramUserId,
            phoneNumber);
        using var firstContact = await PostWebhookAsync(
            contactUpdate,
            WebhookSecret);
        using var duplicateContact = await PostWebhookAsync(
            contactUpdate,
            WebhookSecret);

        Assert.Equal(HttpStatusCode.OK, firstContact.StatusCode);
        Assert.Equal(HttpStatusCode.OK, duplicateContact.StatusCode);
        Assert.Equal(1, factory.OtpSender.SendCount);
        var oneTimeCode = factory.OtpSender.GetCode(challenge.ChallengeId);

        using var statusResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/customer/challenge-status",
            new CustomerChallengeStatusRequest(
                challenge.ChallengeId,
                challenge.ClientChallengeSecret));
        var status = await statusResponse.Content
            .ReadFromJsonAsync<CustomerChallengeStatusResponse>();
        Assert.Equal(CustomerChallengeStatus.OtpSent, status!.Status);

        using var verifyResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/customer/verify-code",
            new VerifyCustomerCodeRequest(
                challenge.ChallengeId,
                oneTimeCode));
        var verification = await verifyResponse.Content
            .ReadFromJsonAsync<CustomerVerificationResponse>();
        using var registrationResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/customer/complete-registration",
            new CompleteCustomerRegistrationRequest(
                verification!.RegistrationToken!,
                "Telegram Customer"));

        Assert.Equal(HttpStatusCode.OK, registrationResponse.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext =
            scope.ServiceProvider.GetRequiredService<MoodPickupDbContext>();
        var storedChallenge = await dbContext.LoginChallenges
            .SingleAsync(item => item.Id == challenge.ChallengeId);
        var customer = await dbContext.Customers.SingleAsync(
            item => item.PhoneNumber == phoneNumber);
        Assert.Equal(telegramUserId, storedChallenge.TelegramUserId);
        Assert.Equal(telegramUserId, storedChallenge.TelegramChatId);
        Assert.NotNull(storedChallenge.TelegramContactVerifiedAt);
        Assert.NotNull(storedChallenge.OtpSentAt);
        Assert.NotEqual(oneTimeCode, storedChallenge.CodeHash);
        Assert.Equal(telegramUserId, customer.TelegramChatId);
        Assert.Equal(
            2,
            await dbContext.TelegramProcessedUpdates.CountAsync());
    }

    [Fact]
    public async Task ContactMismatch_LocksChallengeWithoutGeneratingOtp()
    {
        await factory.ResetAuthenticationStateAsync();
        factory.OtpSender.AllowsUnlinkedPhoneNumbers = false;
        const long telegramUserId = 7_000_000_022;
        var (challenge, _) =
            await RequestLinkAsync("+992900000022");
        using var startResponse = await PostWebhookAsync(
            MessageUpdate(
                1100,
                telegramUserId,
                "private",
                $"/start {GetStartToken(challenge.TelegramBotUrl)}"),
            WebhookSecret);
        startResponse.EnsureSuccessStatusCode();

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            using var mismatch = await PostWebhookAsync(
                ContactUpdate(
                    1100 + attempt,
                    telegramUserId,
                    "+992900999999"),
                WebhookSecret);
            Assert.Equal(HttpStatusCode.OK, mismatch.StatusCode);
        }

        using var statusResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/customer/challenge-status",
            new CustomerChallengeStatusRequest(
                challenge.ChallengeId,
                challenge.ClientChallengeSecret));
        var status = await statusResponse.Content
            .ReadFromJsonAsync<CustomerChallengeStatusResponse>();

        Assert.Equal(CustomerChallengeStatus.Locked, status!.Status);
        Assert.Equal(0, factory.OtpSender.SendCount);
    }

    [Fact]
    public async Task TypedPhoneAndForwardedContact_DoNotVerifyOwnership()
    {
        await factory.ResetAuthenticationStateAsync();
        factory.OtpSender.AllowsUnlinkedPhoneNumbers = false;
        const long telegramUserId = 7_000_000_027;
        const string phoneNumber = "+992900000027";
        var (challenge, _) = await RequestLinkAsync(phoneNumber);
        using var start = await PostWebhookAsync(
            MessageUpdate(
                1150,
                telegramUserId,
                "private",
                $"/start {GetStartToken(challenge.TelegramBotUrl)}"),
            WebhookSecret);
        start.EnsureSuccessStatusCode();
        factory.BotClient.Clear();

        using var typedPhone = await PostWebhookAsync(
            MessageUpdate(
                1151,
                telegramUserId,
                "private",
                phoneNumber),
            WebhookSecret);
        using var forwardedContact = await PostWebhookAsync(
            ContactUpdate(
                1152,
                telegramUserId,
                phoneNumber,
                contactUserId: telegramUserId + 1),
            WebhookSecret);

        Assert.Equal(HttpStatusCode.OK, typedPhone.StatusCode);
        Assert.Equal(HttpStatusCode.OK, forwardedContact.StatusCode);
        Assert.Equal(0, factory.OtpSender.SendCount);
        Assert.Single(factory.BotClient.Messages);

        using var statusResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/customer/challenge-status",
            new CustomerChallengeStatusRequest(
                challenge.ChallengeId,
                challenge.ClientChallengeSecret));
        var status = await statusResponse.Content
            .ReadFromJsonAsync<CustomerChallengeStatusResponse>();
        Assert.Equal(
            CustomerChallengeStatus.WaitingForTelegramContact,
            status!.Status);
    }

    [Fact]
    public async Task ExistingLinkedCustomer_ReceivesOtpWithoutNewContactFlow()
    {
        await factory.ResetAuthenticationStateAsync();
        factory.OtpSender.AllowsUnlinkedPhoneNumbers = false;
        const string phoneNumber = "+992900000028";
        const long telegramChatId = 7_000_000_028;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext =
                scope.ServiceProvider.GetRequiredService<MoodPickupDbContext>();
            dbContext.Customers.Add(new Customer
            {
                Id = Guid.NewGuid(),
                Name = "Linked Customer",
                PhoneNumber = phoneNumber,
                TelegramChatId = telegramChatId,
                CreatedAt = factory.TimeProvider.GetUtcNow(),
                UpdatedAt = factory.TimeProvider.GetUtcNow()
            });
            await dbContext.SaveChangesAsync();
        }

        var (challenge, _) = await RequestLinkAsync(phoneNumber);

        Assert.Equal(CustomerChallengeStatus.OtpSent, challenge.Status);
        Assert.Equal(1, factory.OtpSender.SendCount);
        Assert.Equal(
            $"https://t.me/test_bot",
            challenge.TelegramBotUrl);
    }

    [Fact]
    public async Task GroupContactAndBotMessage_AreAcknowledgedAndIgnored()
    {
        await factory.ResetAuthenticationStateAsync();
        factory.OtpSender.AllowsUnlinkedPhoneNumbers = false;

        using var groupContact = await PostWebhookAsync(
            new
            {
                update_id = 1201L,
                message = new
                {
                    message_id = 1L,
                    from = new { id = 44L, is_bot = false },
                    chat = new { id = -100L, type = "group" },
                    contact = new
                    {
                        phone_number = "+992900000023",
                        user_id = 44L
                    }
                }
            },
            WebhookSecret);
        using var botMessage = await PostWebhookAsync(
            new
            {
                update_id = 1202L,
                message = new
                {
                    message_id = 2L,
                    from = new { id = 45L, is_bot = true },
                    chat = new { id = 45L, type = "private" },
                    text = "/start invalid"
                }
            },
            WebhookSecret);

        Assert.Equal(HttpStatusCode.OK, groupContact.StatusCode);
        Assert.Equal(HttpStatusCode.OK, botMessage.StatusCode);
        Assert.Empty(factory.BotClient.Messages);
        Assert.Equal(0, factory.OtpSender.SendCount);
    }

    [Fact]
    public async Task ExpiredAndReplacedStartTokens_CannotBeUsed()
    {
        await factory.ResetAuthenticationStateAsync();
        factory.OtpSender.AllowsUnlinkedPhoneNumbers = false;
        const string phoneNumber = "+992900000024";

        var (first, _) = await RequestLinkAsync(phoneNumber);
        var firstToken = GetStartToken(first.TelegramBotUrl);
        factory.TimeProvider.Advance(TimeSpan.FromSeconds(61));
        var (replacement, _) = await RequestLinkAsync(phoneNumber);

        using var oldStart = await PostWebhookAsync(
            MessageUpdate(
                1301,
                51L,
                "private",
                $"/start {firstToken}"),
            WebhookSecret);
        Assert.Equal(HttpStatusCode.OK, oldStart.StatusCode);
        Assert.Contains(
            factory.BotClient.Messages,
            message => message.Text.Contains(
                "недействительна",
                StringComparison.OrdinalIgnoreCase));

        factory.TimeProvider.Advance(TimeSpan.FromMinutes(6));
        using var expiredStart = await PostWebhookAsync(
            MessageUpdate(
                1302,
                52L,
                "private",
                $"/start {GetStartToken(replacement.TelegramBotUrl)}"),
            WebhookSecret);
        Assert.Equal(HttpStatusCode.OK, expiredStart.StatusCode);
        Assert.Equal(0, factory.OtpSender.SendCount);
    }

    [Fact]
    public async Task TelegramIdentityConflict_DoesNotOverwriteAnotherCustomer()
    {
        await factory.ResetAuthenticationStateAsync();
        factory.OtpSender.AllowsUnlinkedPhoneNumbers = false;
        const long telegramUserId = 7_000_000_025;

        await CompleteTelegramRegistrationAsync(
            "+992900000025",
            telegramUserId,
            1400);
        factory.TimeProvider.Advance(TimeSpan.FromSeconds(61));
        factory.OtpSender.Clear();
        factory.OtpSender.AllowsUnlinkedPhoneNumbers = false;

        var (secondChallenge, _) =
            await RequestLinkAsync("+992900000026");
        using var secondStart = await PostWebhookAsync(
            MessageUpdate(
                1410,
                telegramUserId,
                "private",
                $"/start {GetStartToken(secondChallenge.TelegramBotUrl)}"),
            WebhookSecret);
        secondStart.EnsureSuccessStatusCode();
        using var conflictingContact = await PostWebhookAsync(
            ContactUpdate(
                1411,
                telegramUserId,
                "+992900000026"),
            WebhookSecret);

        Assert.Equal(HttpStatusCode.OK, conflictingContact.StatusCode);
        Assert.Equal(0, factory.OtpSender.SendCount);
        using var statusResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/customer/challenge-status",
            new CustomerChallengeStatusRequest(
                secondChallenge.ChallengeId,
                secondChallenge.ClientChallengeSecret));
        var status = await statusResponse.Content
            .ReadFromJsonAsync<CustomerChallengeStatusResponse>();
        Assert.Equal(CustomerChallengeStatus.Locked, status!.Status);
    }

    private async Task CompleteTelegramRegistrationAsync(
        string phoneNumber,
        long telegramUserId,
        long updateBase)
    {
        var (challenge, _) = await RequestLinkAsync(phoneNumber);
        using var start = await PostWebhookAsync(
            MessageUpdate(
                updateBase,
                telegramUserId,
                "private",
                $"/start {GetStartToken(challenge.TelegramBotUrl)}"),
            WebhookSecret);
        start.EnsureSuccessStatusCode();
        using var contact = await PostWebhookAsync(
            ContactUpdate(updateBase + 1, telegramUserId, phoneNumber),
            WebhookSecret);
        contact.EnsureSuccessStatusCode();
        var code = factory.OtpSender.GetCode(challenge.ChallengeId);
        using var verify = await _client.PostAsJsonAsync(
            "/api/v1/auth/customer/verify-code",
            new VerifyCustomerCodeRequest(challenge.ChallengeId, code));
        var verification = await verify.Content
            .ReadFromJsonAsync<CustomerVerificationResponse>();
        using var registration = await _client.PostAsJsonAsync(
            "/api/v1/auth/customer/complete-registration",
            new CompleteCustomerRegistrationRequest(
                verification!.RegistrationToken!,
                "First Telegram Customer"));
        registration.EnsureSuccessStatusCode();
    }

    private async Task<(RequestCustomerCodeResponse Response, string Json)>
        RequestLinkAsync(string phoneNumber)
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/customer/request-code",
            new RequestCustomerCodeRequest(phoneNumber));
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Request link failed with {(int)response.StatusCode}: {json}");
        }

        return (
            JsonSerializer.Deserialize<RequestCustomerCodeResponse>(
                json,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Request link response was empty."),
            json);
    }

    private async Task<HttpResponseMessage> PostWebhookAsync(
        object payload,
        string? secret)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/telegram/webhook")
        {
            Content = JsonContent.Create(payload)
        };
        if (secret is not null)
        {
            request.Headers.Add(
                TelegramWebhookSecretFilter.SecretHeaderName,
                secret);
        }

        return await _client.SendAsync(request);
    }

    private static object MessageUpdate(
        long updateId,
        long telegramUserId,
        string chatType,
        string text)
    {
        return new
        {
            update_id = updateId,
            message = new
            {
                message_id = updateId,
                from = new
                {
                    id = telegramUserId,
                    is_bot = false,
                    username = "telegram_user"
                },
                chat = new { id = telegramUserId, type = chatType },
                text
            }
        };
    }

    private static object ContactUpdate(
        long updateId,
        long telegramUserId,
        string phoneNumber,
        long? contactUserId = null)
    {
        return new
        {
            update_id = updateId,
            message = new
            {
                message_id = updateId,
                from = new
                {
                    id = telegramUserId,
                    is_bot = false,
                    username = "telegram_user"
                },
                chat = new { id = telegramUserId, type = "private" },
                contact = new
                {
                    phone_number = phoneNumber,
                    user_id = contactUserId ?? telegramUserId
                }
            }
        };
    }

    private static string GetStartToken(string telegramBotUrl)
    {
        var uri = new Uri(telegramBotUrl);
        const string prefix = "?start=";
        Assert.StartsWith(prefix, uri.Query, StringComparison.Ordinal);
        return Uri.UnescapeDataString(uri.Query[prefix.Length..]);
    }

    private const string WebhookSecret = "testing_webhook_secret";
}
