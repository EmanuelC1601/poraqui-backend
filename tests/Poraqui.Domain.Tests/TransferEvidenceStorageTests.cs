using Microsoft.Extensions.Configuration;
using Poraqui.Infrastructure.Storage;

namespace Poraqui.Domain.Tests;

public sealed class TransferEvidenceStorageTests
{
    [Fact]
    public async Task Save_open_and_delete_use_a_generated_private_reference()
    {
        var root = NewTempRoot();
        try
        {
            var storage = CreateStorage(root);
            var content = "%PDF-1.7\nprivate transfer evidence"u8.ToArray();

            var reference = await storage.SaveAsync(new MemoryStream(content), "bank-details.pdf", "application/pdf",
                CancellationToken.None);

            Assert.StartsWith("transfer-evidence/", reference, StringComparison.Ordinal);
            Assert.DoesNotContain("bank-details", reference, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(root, reference["transfer-evidence/".Length..])));
            var opened = await storage.OpenAsync(reference, CancellationToken.None);
            Assert.NotNull(opened);
            Assert.Equal("application/pdf", opened.Value.ContentType);
            await using (opened.Value.Content)
            using (var copy = new MemoryStream())
            {
                await opened.Value.Content.CopyToAsync(copy);
                Assert.Equal(content, copy.ToArray());
            }

            await storage.DeleteAsync(reference, CancellationToken.None);

            Assert.Null(await storage.OpenAsync(reference, CancellationToken.None));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Theory]
    [InlineData("receipt.pdf", "image/png", "%PDF-1.7")]
    [InlineData("receipt.pdf", "application/pdf", "not a PDF")]
    [InlineData("receipt.exe", "application/octet-stream", "MZ")]
    public async Task Save_rejects_mismatched_type_or_file_signature(string fileName, string contentType, string content)
    {
        var root = NewTempRoot();
        try
        {
            var storage = CreateStorage(root);

            await Assert.ThrowsAsync<ArgumentException>(() => storage.SaveAsync(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)), fileName,
                contentType, CancellationToken.None));

            Assert.Empty(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Theory]
    [InlineData("transfer-evidence/../outside.pdf")]
    [InlineData("transfer-evidence/nested/outside.pdf")]
    [InlineData("transfer-evidence/nested\\outside.pdf")]
    [InlineData("../outside.pdf")]
    [InlineData("outside.pdf")]
    public async Task Open_and_delete_reject_references_outside_storage(string reference)
    {
        var root = NewTempRoot();
        try
        {
            var storage = CreateStorage(root);
            await Assert.ThrowsAsync<ArgumentException>(() => storage.OpenAsync(reference, CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() => storage.DeleteAsync(reference, CancellationToken.None));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static FileTransferEvidenceStorage CreateStorage(string root) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PrivateFiles:Root"] = root
        }).Build());

    private static string NewTempRoot() => Path.Combine(Path.GetTempPath(), "poraqui-evidence-tests", Guid.NewGuid().ToString("N"));

    private static void DeleteTempRoot(string root)
    {
        var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "poraqui-evidence-tests")) + Path.DirectorySeparatorChar;
        var resolved = Path.GetFullPath(root);
        if (Directory.Exists(resolved) && resolved.StartsWith(parent, StringComparison.OrdinalIgnoreCase))
            Directory.Delete(resolved, recursive: true);
    }
}
