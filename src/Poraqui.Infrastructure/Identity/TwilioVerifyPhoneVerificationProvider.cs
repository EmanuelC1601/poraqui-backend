using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Poraqui.Application.Accounts;

namespace Poraqui.Infrastructure.Identity;

public sealed class TwilioVerifyPhoneVerificationProvider(HttpClient http, IConfiguration configuration)
    : IPhoneVerificationProvider
{
    private const string VerifyApi = "https://verify.twilio.com/v2/Services/";

    public async Task StartAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        var serviceSid = RequiredSetting("TwilioVerify:ServiceSid");
        using var request = CreateRequest($"{Uri.EscapeDataString(serviceSid)}/Verifications",
            new Dictionary<string, string> { ["To"] = phoneNumber, ["Channel"] = "sms" });
        using var response = await SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw ProviderFailure(response.StatusCode);
    }

    public async Task<bool> IsApprovedAsync(string phoneNumber, string code, CancellationToken cancellationToken)
    {
        var serviceSid = RequiredSetting("TwilioVerify:ServiceSid");
        using var request = CreateRequest($"{Uri.EscapeDataString(serviceSid)}/VerificationCheck",
            new Dictionary<string, string> { ["To"] = phoneNumber, ["Code"] = code });
        using var response = await SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        if (!response.IsSuccessStatusCode) throw ProviderFailure(response.StatusCode);

        try
        {
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            return document.RootElement.TryGetProperty("status", out var status) &&
                string.Equals(status.GetString(), "approved", StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException exception)
        {
            throw new PhoneVerificationProviderException("The phone verification provider returned an invalid response.", exception);
        }
    }

    private HttpRequestMessage CreateRequest(string path, Dictionary<string, string> form)
    {
        var keySid = RequiredSetting("TwilioVerify:ApiKeySid");
        var keySecret = RequiredSetting("TwilioVerify:ApiKeySecret");
        var request = new HttpRequestMessage(HttpMethod.Post, VerifyApi + path)
        {
            Content = new FormUrlEncodedContent(form)
        };
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keySid}:{keySecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try { return await http.SendAsync(request, cancellationToken); }
        catch (HttpRequestException exception)
        {
            throw new PhoneVerificationProviderException("The phone verification provider could not be reached.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PhoneVerificationProviderException("The phone verification provider timed out.", exception);
        }
    }

    private string RequiredSetting(string key) => !string.IsNullOrWhiteSpace(configuration[key])
        ? configuration[key]!
        : throw new PhoneVerificationProviderException($"Required private configuration '{key}' is missing.");

    private static PhoneVerificationProviderException ProviderFailure(HttpStatusCode statusCode) =>
        new($"Phone verification provider returned HTTP {(int)statusCode}.");
}
