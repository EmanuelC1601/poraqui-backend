using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Poraqui.Application.Accounts;
using Poraqui.Infrastructure.Persistence;

namespace Poraqui.Domain.Tests;

public sealed class ApiTestHost : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly string? _previousConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
    private readonly string? _previousBootstrapKey = Environment.GetEnvironmentVariable("Bootstrap__AdminKey");
    private readonly string? _previousEvidenceRoot = Environment.GetEnvironmentVariable("PrivateFiles__Root");
    private readonly string _evidenceRoot = Path.Combine(Path.GetTempPath(), "poraqui-tests", Guid.NewGuid().ToString("N"));
    public TestPhoneVerificationProvider PhoneVerification { get; } = new();

    public ApiTestHost()
    {
        _connection.Open();
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Server=localhost;Database=unit;Integrated Security=true");
        Environment.SetEnvironmentVariable("Bootstrap__AdminKey", "integration-test-bootstrap-key-with-sufficient-length");
        Environment.SetEnvironmentVariable("PrivateFiles__Root", _evidenceRoot);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPhoneVerificationProvider>();
            services.AddSingleton<IPhoneVerificationProvider>(PhoneVerification);
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        });
    }

    public async Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _previousConnectionString);
            Environment.SetEnvironmentVariable("Bootstrap__AdminKey", _previousBootstrapKey);
            Environment.SetEnvironmentVariable("PrivateFiles__Root", _previousEvidenceRoot);
            var tempEvidenceParent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "poraqui-tests")) + Path.DirectorySeparatorChar;
            var resolvedEvidenceRoot = Path.GetFullPath(_evidenceRoot);
            if (Directory.Exists(resolvedEvidenceRoot) && resolvedEvidenceRoot.StartsWith(tempEvidenceParent, StringComparison.OrdinalIgnoreCase))
                Directory.Delete(resolvedEvidenceRoot, recursive: true);
        }
    }
}

public sealed class TestPhoneVerificationProvider : IPhoneVerificationProvider
{
    public string? LastStartedPhoneNumber { get; private set; }

    public Task StartAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        LastStartedPhoneNumber = phoneNumber;
        return Task.CompletedTask;
    }

    public Task<bool> IsApprovedAsync(string phoneNumber, string code, CancellationToken cancellationToken) =>
        Task.FromResult(phoneNumber == LastStartedPhoneNumber && code == "123456");
}

internal sealed class TestExternalOfferSecretProtector : Poraqui.Application.Orders.IExternalOfferSecretProtector
{
    public string Protect(string secret) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(secret));
    public string Unprotect(string protectedSecret) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(protectedSecret));
}
