using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Poraqui.Domain.Tests;

public sealed class BusinessApiTests
{
    [Fact]
    public async Task Authenticated_business_owners_can_manage_only_their_own_business()
    {
        await using var factory = new ApiTestHost();
        using var client = factory.CreateClient();
        await factory.InitializeDatabaseAsync();
        var routeNames = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .Select(endpoint => endpoint.DisplayName ?? string.Empty).ToArray();
        Assert.Contains(routeNames, route => route.Contains("POST /api/auth/register", StringComparison.Ordinal));

        await RegisterAsync(client, "first-owner@example.test");
        var firstToken = await LoginAsync(client, "first-owner@example.test");
        var firstBusinessId = await RegisterBusinessAsync(client, firstToken, "Taquería Uno");
        firstToken = await LoginAsync(client, "first-owner@example.test");
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, $"api/businesses/{firstBusinessId}", firstToken)).StatusCode);

        await RegisterAsync(client, "second-owner@example.test");
        var secondToken = await LoginAsync(client, "second-owner@example.test");
        var secondBusinessId = await RegisterBusinessAsync(client, secondToken, "Taquería Dos");
        secondToken = await LoginAsync(client, "second-owner@example.test");

        Assert.NotEqual(firstBusinessId, secondBusinessId);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendAsync(client, HttpMethod.Get, $"api/businesses/{firstBusinessId}", secondToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendAsync(client, HttpMethod.Put, $"api/businesses/{firstBusinessId}", secondToken,
                JsonContent.Create(new { name = "Cambio ajeno", address = "Centro" }))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("api/businesses/active")).StatusCode);
    }

    private static async Task RegisterAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("api/auth/register", new { email, password = "Test-Password-1!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("api/auth/login?useCookies=false", new { email, password = "Test-Password-1!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var content = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return content.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static async Task<Guid> RegisterBusinessAsync(HttpClient client, string token, string name)
    {
        using var response = await SendAsync(client, HttpMethod.Post, "api/businesses", token,
            JsonContent.Create(new { name, address = "Domicilio prueba" }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var content = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return content.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path,
        string token, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }
}
