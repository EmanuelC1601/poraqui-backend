using Poraqui.Api.Configuration;

namespace Poraqui.Domain.Tests;

public sealed class PrivateEvidenceStorageConfigurationTests
{
    [Fact]
    public void Production_requires_a_configured_private_root()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            PrivateEvidenceStorageConfiguration.ValidateProductionRoot(null, Path.Combine(Path.GetTempPath(), "wwwroot")));

        Assert.Contains("PrivateFiles:Root", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("wwwroot")]
    [InlineData("wwwroot\\receipts")]
    public void Production_rejects_roots_inside_wwwroot(string relativeRoot)
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "poraqui-storage-config", Guid.NewGuid().ToString("N"));
        var webRoot = Path.Combine(contentRoot, "wwwroot");
        var configuredRoot = relativeRoot == "wwwroot"
            ? webRoot
            : Path.Combine(webRoot, "receipts");

        Assert.Throws<InvalidOperationException>(() =>
            PrivateEvidenceStorageConfiguration.ValidateProductionRoot(configuredRoot, webRoot));
    }

    [Fact]
    public void Production_accepts_a_root_outside_wwwroot_and_returns_its_absolute_path()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "poraqui-storage-config", Guid.NewGuid().ToString("N"));
        var webRoot = Path.Combine(contentRoot, "wwwroot");
        var configuredRoot = Path.Combine(contentRoot, "private-data", "receipts");

        var validatedRoot = PrivateEvidenceStorageConfiguration.ValidateProductionRoot(configuredRoot, webRoot);

        Assert.Equal(Path.GetFullPath(configuredRoot), validatedRoot);
    }
}
