using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;
using MoodPickup.Api.Options;
using MoodPickup.Api.Services;

namespace MoodPickup.Api.Tests;

public sealed class AlifPaymentTests
{
    private const string Key = "44444444";
    private const string Password = "cztef62wrwcysyubbbdnhlk1rs2cztfsqgwww7j0";
    private const string CallbackUrl = "http://myshop.tj/thank_you.php";

    [Fact]
    public void Signatures_MatchThePublishedAlifWebCheckoutVectors()
    {
        var signatures = CreateSignatures();

        Assert.Equal(
            "5f2fea713f2e7999c6351abee2f5f63df1d526cd5bc4e44bf572e442028b01ef",
            signatures.CreatePaymentToken("321123", 2.99m, CallbackUrl));
        Assert.Equal(
            "7432d16897a960d2a7d62cf540972a7ad636c724cef1068f149518ca4cb06880",
            signatures.CreateStatusCheckToken("321123"));
        Assert.True(signatures.VerifyProviderResponseToken(
            "12345678",
            "ok",
            "92938922",
            "75fa87340a0c43a9a0efe9e1aa65f5cab7912e3001714827a5fd481f2d7e0416"));
        Assert.False(signatures.VerifyProviderResponseToken(
            "12345678",
            "ok",
            "92938922",
            "75fa87340a0c43a9a0efe9e1aa65f5cab7912e3001714827a5fd481f2d7e0410"));
        Assert.Equal("2.90", AlifSignatureService.FormatAmount(2.9m));
    }

    [Fact]
    public async Task Launch_ReturnsOnlyThePublishedFormFieldsAndNeverThePassword()
    {
        var options = CreateOptions();
        var signatures = new AlifSignatureService(options);
        var provider = CreateProvider(options, signatures, new StubHandler("{}"));
        var paymentId = Guid.NewGuid();

        var launch = await provider.CreatePaymentLaunchAsync(
            new PaymentProviderLaunchRequest(
                paymentId,
                "321123",
                2.99m,
                "TJS",
                "+992988888888",
                "Mood Pickup order MP-1"),
            CancellationToken.None);

        Assert.Equal(paymentId, launch.PaymentId);
        Assert.Equal("POST", launch.Method);
        Assert.Equal("https://test-web.alif.tj/", launch.ActionUrl);
        Assert.Equal(Key, launch.FormFields["key"]);
        Assert.Equal(
            "5f2fea713f2e7999c6351abee2f5f63df1d526cd5bc4e44bf572e442028b01ef",
            launch.FormFields["token"]);
        Assert.Equal("2.99", launch.FormFields["amount"]);
        Assert.Equal("988888888", launch.FormFields["phone"]);
        Assert.Equal(
            $"https://moodpickup.test/payment/result?paymentId={paymentId:D}",
            launch.FormFields["returnUrl"]);
        Assert.DoesNotContain(
            launch.FormFields,
            item => item.Key.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                    item.Value == Password);
    }

    [Fact]
    public async Task StatusCheck_SendsTheSignedJsonContractAndValidatesTheResponse()
    {
        var handler = new StubHandler(
            """
            {
              "orderId": "12345678",
              "transactionId": "92938922",
              "status": "ok",
              "token": "75fa87340a0c43a9a0efe9e1aa65f5cab7912e3001714827a5fd481f2d7e0416",
              "amount": 10,
              "phone": "+992931234455"
            }
            """);
        var options = CreateOptions();
        var signatures = new AlifSignatureService(options);
        var provider = CreateProvider(options, signatures, handler);

        var result = await provider.CheckPaymentStatusAsync(
            "12345678",
            CancellationToken.None);

        Assert.Equal(PaymentStatus.Paid, result.Status);
        Assert.Equal("92938922", result.TransactionId);
        Assert.Equal(10m, result.Amount);
        Assert.Equal(
            "https://test-web.alif.tj/checktxn",
            handler.RequestUri?.AbsoluteUri);
        Assert.Contains("\"orderId\":\"12345678\"", handler.RequestBody);
        Assert.Contains("\"key\":\"44444444\"", handler.RequestBody);
        Assert.Contains(
            "\"token\":\"ef882af8614e359055c6dcfd9ede305b4825bcac35728dd08e62d394593ceaac\"",
            handler.RequestBody);
    }

    [Fact]
    public async Task StatusCheck_RejectsAnUnverifiableProviderResponse()
    {
        var handler = new StubHandler(
            """
            {
              "orderId": "12345678",
              "transactionId": "92938922",
              "status": "ok",
              "token": "bad",
              "amount": 10
            }
            """);
        var options = CreateOptions();
        var signatures = new AlifSignatureService(options);
        var provider = CreateProvider(options, signatures, handler);

        var exception = await Assert.ThrowsAsync<ApiProblemException>(() =>
            provider.CheckPaymentStatusAsync("12345678", CancellationToken.None));

        Assert.Equal(StatusCodes.Status502BadGateway, exception.Status);
        Assert.Equal("INVALID_PAYMENT_PROVIDER_RESPONSE", exception.Code);
    }

    [Fact]
    public async Task Refund_DoesNotCallAnUndocumentedEndpointOrReportSuccess()
    {
        var handler = new StubHandler("{}");
        var options = CreateOptions();
        var signatures = new AlifSignatureService(options);
        var provider = CreateProvider(options, signatures, handler);

        var exception = await Assert.ThrowsAsync<ApiProblemException>(() =>
            provider.RefundAsync(
                new PaymentProviderRefundRequest("order", "transaction", 10m, "TJS"),
                CancellationToken.None));

        Assert.Equal(StatusCodes.Status501NotImplemented, exception.Status);
        Assert.Equal("PAYMENT_REFUND_PROTOCOL_UNAVAILABLE", exception.Code);
        Assert.Null(handler.RequestUri);
    }

    private static AlifSignatureService CreateSignatures() =>
        new(CreateOptions());

    private static AlifPaymentProvider CreateProvider(
        IOptionsMonitor<AlifOptions> options,
        AlifSignatureService signatures,
        HttpMessageHandler handler)
    {
        var client = new AlifPaymentClient(
            new HttpClient(handler),
            signatures,
            options);
        return new AlifPaymentProvider(signatures, client, options);
    }

    private static StaticOptionsMonitor<AlifOptions> CreateOptions() =>
        new(new AlifOptions
        {
            Enabled = true,
            Environment = "Sandbox",
            Key = Key,
            Password = Password,
            CallbackUrl = CallbackUrl,
            ReturnUrl = "https://moodpickup.test/payment/result",
            Gate = "km"
        });

    private sealed class StubHandler(string responseBody) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    responseBody,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
        where T : class
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
