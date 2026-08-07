using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoodPickup.Api.DTOs.Telegram;
using MoodPickup.Api.Infrastructure.Telegram;
using MoodPickup.Api.Options;

namespace MoodPickup.Api.Tests;

public sealed class TelegramBotApiClientTests
{
    private const string BotToken = "123456:super-secret-test-token";

    [Fact]
    public async Task GetMe_ReturnsTypedBotIdentity()
    {
        var client = CreateClient(
            _ => JsonResponse(
                """
                {"ok":true,"result":{"id":42,"is_bot":true,"username":"test_bot"}}
                """));

        var bot = await client.GetMeAsync(CancellationToken.None);

        Assert.True(bot.IsBot);
        Assert.Equal(42, bot.Id);
        Assert.Equal("test_bot", bot.Username);
    }

    [Fact]
    public async Task SetWebhook_SendsUrlSecretAndOnlyMessageUpdates()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var client = CreateClient(async request =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync();
            return JsonResponse("""{"ok":true,"result":true}""");
        });

        await client.SetWebhookAsync(
            new Uri("https://api.example.test/api/v1/telegram/webhook"),
            "webhook_secret",
            dropPendingUpdates: false,
            CancellationToken.None);

        var requestUri = capturedRequest!.RequestUri!.AbsoluteUri;
        Assert.True(
            requestUri.EndsWith(
                $"/bot{BotToken}/setWebhook",
                StringComparison.Ordinal),
            requestUri);
        Assert.Contains(
            "\"url\":\"https://api.example.test/api/v1/telegram/webhook\"",
            capturedBody,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"secret_token\":\"webhook_secret\"",
            capturedBody,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"allowed_updates\":[\"message\"]",
            capturedBody,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendMessage_SerializesContactKeyboardAsReplyMarkupObject()
    {
        string? capturedBody = null;
        var client = CreateClient(async request =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return JsonResponse(
                """
                {"ok":true,"result":{"message_id":1}}
                """);
        });
        var keyboard = new TelegramReplyKeyboardMarkup(
            [
                [
                    new TelegramKeyboardButton(
                        "Share contact",
                        RequestContact: true)
                ]
            ]);

        await client.SendMessageAsync(
            new TelegramSendMessageRequest(
                123456789,
                "Share your phone number",
                keyboard),
            CancellationToken.None);

        using var document = JsonDocument.Parse(capturedBody!);
        var root = document.RootElement;
        var replyMarkup = root.GetProperty("reply_markup");
        var button = replyMarkup
            .GetProperty("keyboard")[0][0];

        Assert.Equal(JsonValueKind.Object, replyMarkup.ValueKind);
        Assert.Equal("Share contact", button.GetProperty("text").GetString());
        Assert.True(button.GetProperty("request_contact").GetBoolean());
        Assert.True(replyMarkup.GetProperty("resize_keyboard").GetBoolean());
        Assert.True(replyMarkup.GetProperty("one_time_keyboard").GetBoolean());
    }

    [Fact]
    public async Task SendMessage_OmitsReplyMarkupWhenNoMarkupWasProvided()
    {
        string? capturedBody = null;
        var client = CreateClient(async request =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return JsonResponse("""{"ok":true,"result":{"message_id":1}}""");
        });

        await client.SendMessageAsync(
            new TelegramSendMessageRequest(123456789, "Hello"),
            CancellationToken.None);

        using var document = JsonDocument.Parse(capturedBody!);
        Assert.False(document.RootElement.TryGetProperty("reply_markup", out _));
    }

    [Fact]
    public async Task SendMessage_SerializesPlainReplyKeyboardAsObject()
    {
        string? capturedBody = null;
        var client = CreateClient(async request =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return JsonResponse("""{"ok":true,"result":{"message_id":1}}""");
        });
        var keyboard = new TelegramReplyKeyboardMarkup(
            [[new TelegramKeyboardButton("Continue", RequestContact: false)]]);

        await client.SendMessageAsync(
            new TelegramSendMessageRequest(123456789, "Continue", keyboard),
            CancellationToken.None);

        using var document = JsonDocument.Parse(capturedBody!);
        var replyMarkup = document.RootElement.GetProperty("reply_markup");

        Assert.Equal(JsonValueKind.Object, replyMarkup.ValueKind);
        Assert.False(
            replyMarkup
                .GetProperty("keyboard")[0][0]
                .GetProperty("request_contact")
                .GetBoolean());
    }

    [Fact]
    public async Task InvalidToken_MapsToSafeExceptionAndNeverLogsToken()
    {
        var logger = new CapturingLogger<TelegramBotApiClient>();
        var client = CreateClient(
            _ => JsonResponse(
                """{"ok":false,"error_code":401,"description":"Unauthorized 123456:super-secret-test-token"}""",
                HttpStatusCode.Unauthorized),
            logger: logger);

        var exception = await Assert.ThrowsAsync<TelegramApiException>(
            () => client.GetMeAsync(CancellationToken.None));

        Assert.Equal("getMe", exception.MethodName);
        Assert.False(exception.IsRetryable);
        Assert.DoesNotContain(BotToken, exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(
            BotToken,
            string.Join(Environment.NewLine, logger.Messages),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallerCancellation_IsPreserved()
    {
        var client = CreateClient(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException();
        });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.GetMeAsync(cancellation.Token));
    }

    [Fact]
    public async Task Timeout_MapsToRetryableTelegramFailure()
    {
        var client = CreateClient(
            async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException();
            },
            timeout: TimeSpan.FromMilliseconds(20));

        var exception = await Assert.ThrowsAsync<TelegramApiException>(
            () => client.GetMeAsync(CancellationToken.None));

        Assert.True(exception.IsRetryable);
    }

    private static TelegramBotApiClient CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> handler,
        TimeSpan? timeout = null,
        ILogger<TelegramBotApiClient>? logger = null)
    {
        return CreateClient(
            (request, _) => Task.FromResult(handler(request)),
            timeout,
            logger);
    }

    private static TelegramBotApiClient CreateClient(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler,
        TimeSpan? timeout = null,
        ILogger<TelegramBotApiClient>? logger = null)
    {
        return CreateClient(
            (request, _) => handler(request),
            timeout,
            logger);
    }

    private static TelegramBotApiClient CreateClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler,
        TimeSpan? timeout = null,
        ILogger<TelegramBotApiClient>? logger = null)
    {
        var httpClient = new HttpClient(new DelegateHandler(handler))
        {
            BaseAddress = new Uri("https://api.telegram.org/"),
            Timeout = timeout ?? TimeSpan.FromSeconds(5)
        };
        return new TelegramBotApiClient(
            httpClient,
            Microsoft.Extensions.Options.Options.Create(new TelegramOptions
            {
                Enabled = true,
                BotToken = BotToken,
                BotUsername = "test_bot",
                WebhookSecret = "webhook_secret",
                PublicBaseUrl = "https://api.example.test"
            }),
            logger ?? new CapturingLogger<TelegramBotApiClient>());
    }

    private static HttpResponseMessage JsonResponse(
        string json,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json")
        };
    }

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return handler(request, cancellationToken);
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
