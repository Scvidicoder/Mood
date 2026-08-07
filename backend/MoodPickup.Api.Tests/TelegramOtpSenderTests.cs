using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Options;
using MoodPickup.Api.Services;
using MoodPickup.Api.Services.Telegram;

namespace MoodPickup.Api.Tests;

public sealed class TelegramOtpSenderTests
{
    [Fact]
    public async Task RealSender_DeliversOtpButDoesNotWriteItToLogs()
    {
        const string oneTimeCode = "123456";
        var options = Microsoft.Extensions.Options.Options.Create(
            new TelegramOptions
            {
                Enabled = true,
                BotUsername = "test_bot",
                OtpMessageTemplate = "Your Mood Pickup code is {0}."
            });
        var botClient = new TestTelegramBotClient();
        var logger = new CapturingLogger<TelegramOtpSender>();
        var sender = new TelegramOtpSender(
            botClient,
            new TelegramMessageProvider(options),
            logger);
        var challenge = new LoginChallenge
        {
            Id = Guid.NewGuid(),
            TelegramChatId = 7_000_000_029
        };

        await sender.SendAsync(
            challenge,
            oneTimeCode,
            CancellationToken.None);

        Assert.Contains(
            oneTimeCode,
            Assert.Single(botClient.Messages).Text,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            oneTimeCode,
            string.Join(Environment.NewLine, logger.Messages),
            StringComparison.Ordinal);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

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
