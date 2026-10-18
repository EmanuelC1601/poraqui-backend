using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Poraqui.Domain.Tests;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task Readiness_checks_database_connectivity_without_requiring_authentication()
    {
        await using var factory = new ApiTestHost();
        using var client = factory.CreateClient();
        await factory.InitializeDatabaseAsync();

        using var live = await client.GetAsync("/health/live");
        using var ready = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal("ready", (await ready.Content.ReadFromJsonAsync<HealthResponse>())?.Status);
    }

    [Fact]
    public async Task Readiness_returns_service_unavailable_when_sql_is_not_configured()
    {
        await using var factory = new NoDatabaseApiTestHost();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private sealed record HealthResponse(string Status);

    private sealed class NoDatabaseApiTestHost : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = string.Empty }));
        }
    }
}
