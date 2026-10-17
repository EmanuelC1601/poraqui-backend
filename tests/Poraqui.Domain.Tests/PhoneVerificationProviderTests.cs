using System.Net;
using Microsoft.Extensions.Configuration;
using Poraqui.Application.Accounts;
using Poraqui.Infrastructure.Identity;

namespace Poraqui.Domain.Tests;

public sealed class PhoneVerificationProviderTests
{
    [Fact]
    public async Task Twilio_verify_sends_sms_and_checks_the_submitted_code()
    {
        var requests = new List<(Uri Uri, string Authorization, string Body)>();
        var responses = new Queue<HttpResponseMessage>([
            Json(HttpStatusCode.Created, "{\"status\":\"pending\"}"),
            Json(HttpStatusCode.OK, "{\"status\":\"pending\"}"),
            Json(HttpStatusCode.OK, "{\"status\":\"approved\"}"),
            Json(HttpStatusCode.NotFound, "{}")
        ]);
        using var http = new HttpClient(new RecordingHandler((request, body) =>
        {
            requests.Add((request.RequestUri!, request.Headers.Authorization!.Scheme, body));
            return responses.Dequeue();
        }));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TwilioVerify:ServiceSid"] = "VA00000000000000000000000000000000",
            ["TwilioVerify:ApiKeySid"] = "SK00000000000000000000000000000000",
            ["TwilioVerify:ApiKeySecret"] = "test-secret"
        }).Build();
        var provider = new TwilioVerifyPhoneVerificationProvider(http, configuration);

        await provider.StartAsync("+525512345678", CancellationToken.None);
        Assert.False(await provider.IsApprovedAsync("+525512345678", "000000", CancellationToken.None));
        Assert.True(await provider.IsApprovedAsync("+525512345678", "123456", CancellationToken.None));
        Assert.False(await provider.IsApprovedAsync("+525512345678", "123456", CancellationToken.None));

        Assert.Equal("https://verify.twilio.com/v2/Services/VA00000000000000000000000000000000/Verifications", requests[0].Uri.ToString());
        Assert.Contains("To=%2B525512345678", requests[0].Body);
        Assert.Contains("Channel=sms", requests[0].Body);
        Assert.Equal("Basic", requests[0].Authorization);
        Assert.EndsWith("/VerificationCheck", requests[1].Uri.AbsolutePath);
        Assert.Contains("Code=000000", requests[1].Body);
        Assert.Contains("Code=123456", requests[2].Body);
    }

    [Fact]
    public async Task Twilio_verify_requires_private_credentials_without_exposing_values()
    {
        using var http = new HttpClient(new RecordingHandler((_, _) => throw new InvalidOperationException("Should not send a request.")));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TwilioVerify:ServiceSid"] = "VA00000000000000000000000000000000"
        }).Build();
        var provider = new TwilioVerifyPhoneVerificationProvider(http, configuration);

        var error = await Assert.ThrowsAsync<PhoneVerificationProviderException>(() =>
            provider.StartAsync("+525512345678", CancellationToken.None));

        Assert.Contains("TwilioVerify:ApiKeySid", error.Message);
        Assert.DoesNotContain("test-secret", error.ToString());
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
    };

    private sealed class RecordingHandler(Func<HttpRequestMessage, string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request, body);
        }
    }
}
