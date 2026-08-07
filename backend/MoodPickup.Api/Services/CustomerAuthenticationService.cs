using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MoodPickup.Api.Data;
using MoodPickup.Api.DTOs;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Extensions;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Infrastructure.Telegram;
using MoodPickup.Api.Interfaces;
using MoodPickup.Api.Options;

namespace MoodPickup.Api.Services;

public sealed class CustomerAuthenticationService(
    MoodPickupDbContext dbContext,
    AuthenticationHashing hashing,
    ITelegramOtpSender telegramOtpSender,
    ITokenIssuer tokenIssuer,
    RefreshTokenService refreshTokenService,
    IOptions<OtpOptions> otpOptions,
    IOptions<TelegramOptions> telegramOptions,
    TimeProvider timeProvider,
    ILogger<CustomerAuthenticationService> logger)
{
    private readonly OtpOptions _otpOptions = otpOptions.Value;
    private readonly TelegramOptions _telegramOptions = telegramOptions.Value;

    public async Task<RequestCustomerCodeResponse> RequestCodeAsync(
        RequestCustomerCodeRequest request,
        AuthenticationRequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (!PhoneNumberNormalizer.TryNormalize(
                request.PhoneNumber,
                out var phoneNumber))
        {
            throw new ApiProblemException(
                StatusCodes.Status400BadRequest,
                "invalid_phone_number",
                "Phone number is invalid",
                "INVALID_PHONE_NUMBER");
        }

        var maskedPhone = PhoneNumberMasker.Mask(phoneNumber);
        var ipHash = hashing.HashMetadata(metadata.IpAddress);
        var customer = await dbContext.Customers
            .SingleOrDefaultAsync(
                existingCustomer => existingCustomer.PhoneNumber == phoneNumber,
                cancellationToken);

        if (!telegramOtpSender.AllowsUnlinkedPhoneNumbers &&
            !_telegramOptions.Enabled)
        {
            throw new ApiProblemException(
                StatusCodes.Status503ServiceUnavailable,
                "telegram_not_configured",
                "Telegram authentication is not configured",
                "TELEGRAM_NOT_CONFIGURED");
        }

        var hourAgo = now.AddHours(-1);
        var phoneRequestCount = await dbContext.LoginChallenges
            .CountAsync(
                challenge =>
                    challenge.PhoneNumber == phoneNumber &&
                    challenge.CreatedAt >= hourAgo,
                cancellationToken);
        var ipRequestCount = await dbContext.LoginChallenges
            .CountAsync(
                challenge =>
                    challenge.RequestIpHash == ipHash &&
                    challenge.CreatedAt >= hourAgo,
                cancellationToken);
        var telegramRequestCount = customer?.TelegramChatId is long telegramChatId
            ? await dbContext.LoginChallenges.CountAsync(
                challenge =>
                    challenge.TelegramChatId == telegramChatId &&
                    challenge.CreatedAt >= hourAgo,
                cancellationToken)
            : 0;
        var lastChallenge = await dbContext.LoginChallenges
            .Where(challenge => challenge.PhoneNumber == phoneNumber)
            .OrderByDescending(challenge => challenge.LastSentAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (phoneRequestCount >= _otpOptions.PhoneRequestsPerHour ||
            ipRequestCount >= _otpOptions.IpRequestsPerHour ||
            telegramRequestCount >= _otpOptions.TelegramChatRequestsPerHour ||
            lastChallenge is not null &&
            lastChallenge.LastSentAt.AddSeconds(_otpOptions.ResendDelaySeconds) > now)
        {
            logger.LogWarning(
                "OTP request rate limit reached for phone {MaskedPhone}.",
                maskedPhone);
            throw new ApiProblemException(
                StatusCodes.Status429TooManyRequests,
                "too_many_code_requests",
                "Too many verification-code requests",
                "TOO_MANY_CODE_REQUESTS",
                "Please wait before requesting another code.");
        }

        var activeChallenges = await dbContext.LoginChallenges
            .Where(challenge =>
                challenge.PhoneNumber == phoneNumber &&
                !challenge.IsUsed)
            .ToListAsync(cancellationToken);

        foreach (var activeChallenge in activeChallenges)
        {
            activeChallenge.IsUsed = true;
        }

        var challengeId = Guid.NewGuid();
        var clientChallengeSecret = AuthenticationHashing.CreateRandomToken(32);
        var sendImmediately =
            telegramOtpSender.AllowsUnlinkedPhoneNumbers ||
            customer?.TelegramChatId is not null;
        var oneTimeCode = sendImmediately
            ? RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6")
            : null;
        var linkToken = sendImmediately
            ? null
            : AuthenticationHashing.CreateRandomToken(32);
        var lifetimeMinutes = sendImmediately
            ? _otpOptions.LifetimeMinutes
            : _telegramOptions.LinkExpirationMinutes;
        var challenge = new LoginChallenge
        {
            Id = challengeId,
            PhoneNumber = phoneNumber,
            CodeHash = oneTimeCode is null
                ? null
                : hashing.HashOneTimeCode(challengeId, oneTimeCode),
            TelegramChatId = customer?.TelegramChatId,
            TelegramUserId = customer?.TelegramChatId,
            TelegramLinkTokenHash = linkToken is null
                ? null
                : hashing.HashTelegramLinkToken(linkToken),
            TelegramLinkExpiresAt = linkToken is null
                ? null
                : now.AddMinutes(_telegramOptions.LinkExpirationMinutes),
            ClientStatusSecretHash =
                hashing.HashClientChallengeSecret(clientChallengeSecret),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(lifetimeMinutes),
            MaximumAttempts = _otpOptions.MaximumAttempts,
            LastSentAt = now,
            Purpose = customer is null
                ? LoginChallengePurpose.Registration
                : LoginChallengePurpose.Login,
            RequestIpHash = ipHash,
            UserAgentHash = hashing.HashMetadata(metadata.UserAgent)
        };

        dbContext.LoginChallenges.Add(challenge);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (oneTimeCode is not null)
        {
            try
            {
                await telegramOtpSender.SendAsync(
                    challenge,
                    oneTimeCode,
                    cancellationToken);
                challenge.OtpSentAt = timeProvider.GetUtcNow();
                await dbContext.SaveChangesAsync(cancellationToken);
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
                throw new ApiProblemException(
                    StatusCodes.Status503ServiceUnavailable,
                    "telegram_delivery_failed",
                    "Telegram could not deliver the verification code",
                    "TELEGRAM_DELIVERY_FAILED");
            }
        }

        logger.LogInformation(
            "Customer authentication challenge created for phone {MaskedPhone}, challenge {ChallengeId}.",
            maskedPhone,
            challenge.Id);

        return new RequestCustomerCodeResponse(
            challenge.Id,
            checked((int)(challenge.ExpiresAt - now).TotalSeconds),
            _otpOptions.ResendDelaySeconds,
            _telegramOptions.BuildBotUrl(linkToken),
            clientChallengeSecret,
            sendImmediately
                ? CustomerChallengeStatus.OtpSent
                : CustomerChallengeStatus.WaitingForTelegramStart);
    }

    public async Task<CustomerChallengeStatusResponse> GetChallengeStatusAsync(
        CustomerChallengeStatusRequest request,
        CancellationToken cancellationToken)
    {
        var challenge = await dbContext.LoginChallenges
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == request.ChallengeId,
                cancellationToken);
        if (challenge?.ClientStatusSecretHash is null)
        {
            throw InvalidChallengeStatusSecret();
        }

        var suppliedHash = hashing.HashClientChallengeSecret(
            request.ClientChallengeSecret);
        if (!AuthenticationHashing.FixedTimeEquals(
                challenge.ClientStatusSecretHash,
                suppliedHash))
        {
            throw InvalidChallengeStatusSecret();
        }

        var now = timeProvider.GetUtcNow();
        var status = GetStatus(challenge, now);
        return new CustomerChallengeStatusResponse(
            status,
            Math.Max(
                0,
                checked((int)Math.Ceiling(
                    (challenge.ExpiresAt - now).TotalSeconds))),
            challenge.LastSentAt
                .AddSeconds(_otpOptions.ResendDelaySeconds) <= now);
    }

    public async Task<CustomerAuthenticationResult> VerifyCodeAsync(
        VerifyCustomerCodeRequest request,
        AuthenticationRequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var challenge = await dbContext.LoginChallenges
            .SingleOrDefaultAsync(
                existingChallenge => existingChallenge.Id == request.ChallengeId,
                cancellationToken);

        if (challenge is null ||
            challenge.IsUsed ||
            challenge.CodeHash is null ||
            challenge.OtpSentAt is null)
        {
            throw InvalidCode();
        }

        if (challenge.ExpiresAt <= now)
        {
            challenge.IsUsed = true;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new ApiProblemException(
                StatusCodes.Status410Gone,
                "code_expired",
                "The verification code has expired",
                "CODE_EXPIRED");
        }

        if (challenge.AttemptCount >= challenge.MaximumAttempts)
        {
            throw TooManyAttempts();
        }

        var submittedHash = hashing.HashOneTimeCode(challenge.Id, request.Code);

        if (!AuthenticationHashing.FixedTimeEquals(challenge.CodeHash, submittedHash))
        {
            challenge.AttemptCount++;
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogWarning(
                "OTP verification failed for challenge {ChallengeId}. Attempt {AttemptCount}.",
                challenge.Id,
                challenge.AttemptCount);

            if (challenge.AttemptCount >= challenge.MaximumAttempts)
            {
                throw TooManyAttempts();
            }

            throw InvalidCode();
        }

        challenge.IsUsed = true;
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "OTP verification succeeded for challenge {ChallengeId}.",
            challenge.Id);

        var customer = await dbContext.Customers
            .SingleOrDefaultAsync(
                existingCustomer => existingCustomer.PhoneNumber == challenge.PhoneNumber,
                cancellationToken);

        if (customer is null)
        {
            return CustomerAuthenticationResult.ForRegistration(
                tokenIssuer.IssueRegistrationToken(challenge.PhoneNumber, challenge.Id));
        }

        var accessToken = tokenIssuer.IssueCustomerAccessToken(customer);
        var refreshToken = await refreshTokenService.IssueAsync(
            AccountType.Customer,
            customer.Id,
            metadata,
            cancellationToken);

        return CustomerAuthenticationResult.ForCustomer(
            accessToken,
            refreshToken,
            customer);
    }

    public async Task<CustomerAuthenticationResult> CompleteRegistrationAsync(
        CompleteCustomerRegistrationRequest request,
        AuthenticationRequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var registrationClaims = tokenIssuer.ValidateRegistrationToken(
            request.RegistrationToken);
        var challenge = await dbContext.LoginChallenges
            .SingleOrDefaultAsync(
                existingChallenge =>
                    existingChallenge.Id == registrationClaims.ChallengeId &&
                    existingChallenge.PhoneNumber == registrationClaims.PhoneNumber,
                cancellationToken);

        if (challenge is null ||
            !challenge.IsUsed ||
            challenge.Purpose != LoginChallengePurpose.Registration)
        {
            throw new ApiProblemException(
                StatusCodes.Status401Unauthorized,
                "invalid_registration_token",
                "Registration session is invalid or expired",
                "INVALID_REGISTRATION_TOKEN");
        }

        var existingCustomer = await dbContext.Customers
            .AnyAsync(
                customer => customer.PhoneNumber == registrationClaims.PhoneNumber,
                cancellationToken);

        if (existingCustomer)
        {
            throw new ApiProblemException(
                StatusCodes.Status409Conflict,
                "phone_already_registered",
                "This phone number is already registered",
                "PHONE_ALREADY_REGISTERED");
        }

        if (!telegramOtpSender.AllowsUnlinkedPhoneNumbers &&
            (challenge.TelegramContactVerifiedAt is null ||
             challenge.TelegramChatId is null))
        {
            throw new ApiProblemException(
                StatusCodes.Status409Conflict,
                "telegram_contact_required",
                "Telegram contact verification is required",
                "TELEGRAM_CONTACT_REQUIRED");
        }

        if (challenge.TelegramChatId is long telegramChatId &&
            await dbContext.Customers.AnyAsync(
                customer => customer.TelegramChatId == telegramChatId,
                cancellationToken))
        {
            throw new ApiProblemException(
                StatusCodes.Status409Conflict,
                "telegram_identity_conflict",
                "Telegram identity cannot be linked",
                "TELEGRAM_IDENTITY_CONFLICT");
        }

        var now = timeProvider.GetUtcNow();
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            PhoneNumber = registrationClaims.PhoneNumber,
            TelegramChatId = challenge.TelegramChatId,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync(cancellationToken);

        var accessToken = tokenIssuer.IssueCustomerAccessToken(customer);
        var refreshToken = await refreshTokenService.IssueAsync(
            AccountType.Customer,
            customer.Id,
            metadata,
            cancellationToken);

        return CustomerAuthenticationResult.ForCustomer(
            accessToken,
            refreshToken,
            customer);
    }

    private static ApiProblemException InvalidCode()
    {
        return new ApiProblemException(
            StatusCodes.Status400BadRequest,
            "invalid_code",
            "Invalid or expired code",
            "INVALID_CODE");
    }

    private static ApiProblemException TooManyAttempts()
    {
        return new ApiProblemException(
            StatusCodes.Status429TooManyRequests,
            "too_many_attempts",
            "Too many verification attempts",
            "TOO_MANY_ATTEMPTS");
    }

    private static ApiProblemException InvalidChallengeStatusSecret()
    {
        return new ApiProblemException(
            StatusCodes.Status401Unauthorized,
            "invalid_challenge_status_secret",
            "Challenge status authentication failed",
            "INVALID_CHALLENGE_STATUS_SECRET");
    }

    private CustomerChallengeStatus GetStatus(
        LoginChallenge challenge,
        DateTimeOffset now)
    {
        if (challenge.IsUsed)
        {
            return CustomerChallengeStatus.Completed;
        }
        if (challenge.TelegramDeliveryFailedAt is not null ||
            challenge.TelegramLinkAttemptCount >=
            _telegramOptions.MaximumContactMismatchAttempts)
        {
            return CustomerChallengeStatus.Locked;
        }
        if (challenge.ExpiresAt <= now)
        {
            return CustomerChallengeStatus.Expired;
        }
        if (challenge.OtpSentAt is not null)
        {
            return CustomerChallengeStatus.OtpSent;
        }
        return challenge.TelegramStartedAt is null
            ? CustomerChallengeStatus.WaitingForTelegramStart
            : CustomerChallengeStatus.WaitingForTelegramContact;
    }
}

public sealed record CustomerAuthenticationResult(
    bool IsNewCustomer,
    IssuedAccessToken? AccessToken,
    IssuedRefreshToken? RefreshToken,
    Customer? Customer,
    string? RegistrationToken)
{
    public static CustomerAuthenticationResult ForRegistration(string registrationToken)
        => new(true, null, null, null, registrationToken);

    public static CustomerAuthenticationResult ForCustomer(
        IssuedAccessToken accessToken,
        IssuedRefreshToken refreshToken,
        Customer customer)
        => new(false, accessToken, refreshToken, customer, null);
}
