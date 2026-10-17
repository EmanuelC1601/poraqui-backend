using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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

        using var productResponse = await SendAsync(client, HttpMethod.Post, $"api/businesses/{firstBusinessId}/products", firstToken,
            JsonContent.Create(new { name = "Producto ajeno", price = 10m }));
        Assert.Equal(HttpStatusCode.Created, productResponse.StatusCode);
        using var productJson = JsonDocument.Parse(await productResponse.Content.ReadAsStringAsync());
        var productId = productJson.RootElement.GetProperty("id").GetGuid();

        Assert.NotEqual(firstBusinessId, secondBusinessId);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendAsync(client, HttpMethod.Get, $"api/businesses/{firstBusinessId}", secondToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendAsync(client, HttpMethod.Put, $"api/businesses/{firstBusinessId}", secondToken,
                JsonContent.Create(new { name = "Cambio ajeno", address = "Centro" }))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendAsync(client, HttpMethod.Put, $"api/products/{productId}", secondToken,
                JsonContent.Create(new { name = "Cambio ajeno", price = 1m, isAvailable = false }))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("api/businesses/active")).StatusCode);
    }

    [Fact]
    public async Task Order_prices_are_loaded_from_catalog_and_delivery_rate_on_server()
    {
        await using var factory = new ApiTestHost();
        using var client = factory.CreateClient();
        await factory.InitializeDatabaseAsync();

        var bootstrap = await client.PostAsJsonAsync("api/auth/bootstrap-admin", new
        {
            email = "admin@example.test", displayName = "Test Admin", password = "Test-Password-1!",
            bootstrapKey = "integration-test-bootstrap-key-with-sufficient-length"
        });
        Assert.Equal(HttpStatusCode.Created, bootstrap.StatusCode);
        var adminToken = await LoginAsync(client, "admin@example.test");

        await RegisterAsync(client, "merchant@example.test");
        var merchantToken = await LoginAsync(client, "merchant@example.test");
        var businessId = await RegisterBusinessAsync(client, merchantToken, "Mercado de prueba");
        merchantToken = await LoginAsync(client, "merchant@example.test");
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Put,
            $"api/businesses/{businessId}/active", adminToken, JsonContent.Create(new { active = true }))).StatusCode);

        using var zoneResponse = await SendAsync(client, HttpMethod.Post, "api/delivery-zones", adminToken,
            JsonContent.Create(new { name = "Centro de prueba" }));
        Assert.Equal(HttpStatusCode.OK, zoneResponse.StatusCode);
        using var zoneJson = JsonDocument.Parse(await zoneResponse.Content.ReadAsStringAsync());
        var zoneId = zoneJson.RootElement.GetProperty("id").GetGuid();

        using var productResponse = await SendAsync(client, HttpMethod.Post, $"api/businesses/{businessId}/products", merchantToken,
            JsonContent.Create(new { name = "Producto de prueba", price = 25.50m }));
        Assert.Equal(HttpStatusCode.Created, productResponse.StatusCode);
        using var productJson = JsonDocument.Parse(await productResponse.Content.ReadAsStringAsync());
        var productId = productJson.RootElement.GetProperty("id").GetGuid();

        using var rateResponse = await SendAsync(client, HttpMethod.Post, $"api/businesses/{businessId}/delivery-rates", merchantToken,
            JsonContent.Create(new { zoneId, fee = 18.25m }));
        Assert.Equal(HttpStatusCode.OK, rateResponse.StatusCode);

        await RegisterAsync(client, "customer@example.test");
        var customerToken = await LoginAsync(client, "customer@example.test");
        var requestJson = $$"""{"businessId":"{{businessId}}","zoneId":"{{zoneId}}","deliveryAddress":"Calle de prueba","items":[{"productId":"{{productId}}","quantity":2,"unitPrice":0.01}]}""";
        using var orderResponse = await SendAsync(client, HttpMethod.Post, "api/orders", customerToken,
            new StringContent(requestJson, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Created, orderResponse.StatusCode);
        using var orderJson = JsonDocument.Parse(await orderResponse.Content.ReadAsStringAsync());
        Assert.Equal(51.00m, orderJson.RootElement.GetProperty("subtotal").GetDecimal());
        Assert.Equal(18.25m, orderJson.RootElement.GetProperty("deliveryFee").GetDecimal());
        Assert.Equal(69.25m, orderJson.RootElement.GetProperty("total").GetDecimal());
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
