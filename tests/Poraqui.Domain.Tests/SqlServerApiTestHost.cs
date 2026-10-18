using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Poraqui.Application.Accounts;
using Poraqui.Infrastructure.Persistence;

namespace Poraqui.Domain.Tests;

internal sealed class SqlServerApiTestHost : WebApplicationFactory<Program>
{
    private readonly string _connectionString = Environment.GetEnvironmentVariable(
        SqlServerIntegrationFactAttribute.ConnectionStringEnvironmentVariable)!;
    private readonly string _evidenceRoot = Path.Combine(Path.GetTempPath(), "poraqui-sql-tests", Guid.NewGuid().ToString("N"));

    public const string BootstrapKey = "isolated-sql-integration-bootstrap-key";
    public TestPhoneVerificationProvider PhoneVerification { get; } = new();

    public HttpClient CreateIsolatedClient()
    {
        var previousConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        var previousBootstrapKey = Environment.GetEnvironmentVariable("Bootstrap__AdminKey");
        var previousEvidenceRoot = Environment.GetEnvironmentVariable("PrivateFiles__Root");
        try
        {
            // Program reads these values before WebApplicationFactory applies service overrides.
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _connectionString);
            Environment.SetEnvironmentVariable("Bootstrap__AdminKey", BootstrapKey);
            Environment.SetEnvironmentVariable("PrivateFiles__Root", _evidenceRoot);
            return CreateClient();
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", previousConnectionString);
            Environment.SetEnvironmentVariable("Bootstrap__AdminKey", previousBootstrapKey);
            Environment.SetEnvironmentVariable("PrivateFiles__Root", previousEvidenceRoot);
        }
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
            services.AddDbContext<AppDbContext>(options => options.UseSqlServer(_connectionString));
        });
    }

    public async Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "poraqui-sql-tests")) + Path.DirectorySeparatorChar;
            var root = Path.GetFullPath(_evidenceRoot);
            if (Directory.Exists(root) && root.StartsWith(parent, StringComparison.OrdinalIgnoreCase))
                Directory.Delete(root, recursive: true);
        }
    }
}
