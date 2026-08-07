using System.Globalization;
using Microsoft.Extensions.Options;
using MoodPickup.Api.Options;

namespace MoodPickup.Api.Services.Telegram;

public sealed class TelegramMessageProvider(IOptions<TelegramOptions> options)
{
    private readonly TelegramOptions _options = options.Value;

    public string Welcome =>
        "Здравствуйте! Этот бот подтверждает вход в Mood Pickup.\n\n" +
        "Вернитесь на сайт, введите номер телефона и нажмите «Получить код в Telegram».";

    public string Help =>
        "Чтобы войти в Mood Pickup, начните на сайте и откройте выданную ссылку на этого бота. " +
        "Затем поделитесь своим номером через кнопку Telegram.";

    public string ShareContact =>
        "Поделитесь номером телефона через кнопку ниже. " +
        "Номер должен совпадать с номером, указанным на сайте.";

    public string InvalidOrExpiredLink =>
        "Ссылка недействительна или устарела. Вернитесь на сайт и запросите новую.";

    public string PhoneMismatch =>
        "Этот номер не совпадает с номером на сайте. Вернитесь на сайт и укажите тот же номер.";

    public string TooManyAttempts =>
        "Слишком много неудачных попыток. Вернитесь на сайт и запросите новую ссылку.";

    public string IdentityConflict =>
        "Не удалось безопасно связать этот Telegram-аккаунт. " +
        "Вернитесь на сайт и попробуйте позже.";

    public string GenericError =>
        "Не удалось завершить подтверждение. Вернитесь на сайт и запросите новую ссылку.";

    public string ContactButton => "Поделиться номером";

    public string Otp(string oneTimeCode)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            _options.OtpMessageTemplate,
            oneTimeCode);
    }
}
