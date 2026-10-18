using Poraqui.Api.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace Poraqui.Domain.Tests;

public sealed class PersistentDataProtectionConfigurationTests
{
    [Fact]
    public void Production_requires_a_persistent_key_path()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            PersistentDataProtectionConfiguration.ValidateProductionPath(null, Path.Combine(Path.GetTempPath(), "wwwroot")));

        Assert.Contains("DataProtection:KeysPath", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("wwwroot")]
    [InlineData("wwwroot\\keys")]
    public void Production_rejects_key_paths_inside_wwwroot(string relativePath)
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "poraqui-data-protection", Guid.NewGuid().ToString("N"));
        var webRoot = Path.Combine(contentRoot, "wwwroot");
        var configuredPath = relativePath == "wwwroot" ? webRoot : Path.Combine(webRoot, "keys");

        Assert.Throws<InvalidOperationException>(() =>
            PersistentDataProtectionConfiguration.ValidateProductionPath(configuredPath, webRoot));
    }

    [Fact]
    public void Production_accepts_a_private_key_path_outside_wwwroot()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "poraqui-data-protection", Guid.NewGuid().ToString("N"));
        var webRoot = Path.Combine(contentRoot, "wwwroot");
        var configuredPath = Path.Combine(contentRoot, "persistent", "keys");

        var validatedPath = PersistentDataProtectionConfiguration.ValidateProductionPath(configuredPath, webRoot);

        Assert.Equal(Path.GetFullPath(configuredPath), validatedPath);
    }

    [Fact]
    public void Persisted_data_protection_keys_can_unprotect_codes_after_provider_restart()
    {
        var keyDirectory = Path.Combine(Path.GetTempPath(), "poraqui-data-protection-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(keyDirectory);
        try
        {
            string protectedCode;
            using (var firstProvider = CreateServiceProvider(keyDirectory))
                protectedCode = firstProvider.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("courier-pickup-code").Protect("842913");

            using var restartedProvider = CreateServiceProvider(keyDirectory);
            Assert.Equal("842913", restartedProvider.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("courier-pickup-code").Unprotect(protectedCode));
        }
        finally
        {
            Directory.Delete(keyDirectory, recursive: true);
        }
    }

    private static ServiceProvider CreateServiceProvider(string keyDirectory) => new ServiceCollection()
        .AddDataProtection()
        .SetApplicationName("Poraqui.DataProtection.Tests")
        .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory))
        .Services
        .BuildServiceProvider();
}
