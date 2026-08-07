using System.Collections.Concurrent;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Tests;

public sealed class TestTelegramOtpSender : ITelegramOtpSender
{
    private readonly ConcurrentDictionary<Guid, string> _codes = new();

    public bool AllowsUnlinkedPhoneNumbers { get; set; } = true;

    public Task SendAsync(
        LoginChallenge challenge,
        string oneTimeCode,
        CancellationToken cancellationToken)
    {
        _codes[challenge.Id] = oneTimeCode;
        return Task.CompletedTask;
    }

    public string GetCode(Guid challengeId)
    {
        return _codes.TryGetValue(challengeId, out var code)
            ? code
            : throw new InvalidOperationException(
                $"No test OTP was captured for challenge {challengeId}.");
    }

    public void Clear()
    {
        _codes.Clear();
        AllowsUnlinkedPhoneNumbers = true;
    }

    public int SendCount => _codes.Count;
}
