using Poraqui.Application.Orders;
using Microsoft.Extensions.Configuration;

namespace Poraqui.Infrastructure.Storage;

public sealed class FileTransferEvidenceStorage : ITransferEvidenceStorage
{
    private const long MaxFileSize = 8 * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, string> AllowedTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".pdf"] = "application/pdf"
    };

    private readonly string _root;

    public FileTransferEvidenceStorage(IConfiguration configuration)
    {
        var root = configuration["PrivateFiles:Root"];
        _root = Path.GetFullPath(string.IsNullOrWhiteSpace(root)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Poraqui", "private-files")
            : root);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var extension = Path.GetExtension(fileName);
        if (!AllowedTypes.TryGetValue(extension, out var expectedType) ||
            !string.Equals(expectedType, contentType, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Evidence must be a PDF, JPEG, or PNG file with a matching content type.", nameof(fileName));

        var key = $"transfer-evidence/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var bufferStream = new MemoryStream();
        var buffer = new byte[81920];
        long total = 0;
        while (true)
        {
            var count = await content.ReadAsync(buffer, cancellationToken);
            if (count == 0) break;
            total += count;
            if (total > MaxFileSize)
                throw new ArgumentException("Evidence file exceeds the 8 MB limit.", nameof(content));
            await bufferStream.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
        }
        var bytes = bufferStream.ToArray();
        if (bytes.Length == 0)
            throw new ArgumentException("Evidence file is empty.", nameof(content));
        if (!HasExpectedSignature(extension, bytes))
            throw new ArgumentException("Evidence file signature does not match its content type.", nameof(content));

        await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920,
                         FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            await output.WriteAsync(bytes, cancellationToken);
        }
        return key;
    }

    public Task<(Stream Content, string ContentType)?> OpenAsync(string reference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Resolve(reference);
        if (!File.Exists(path)) return Task.FromResult<(Stream Content, string ContentType)?>(null);
        var extension = Path.GetExtension(path);
        if (!AllowedTypes.TryGetValue(extension, out var contentType))
            return Task.FromResult<(Stream Content, string ContentType)?>(null);
        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult<(Stream Content, string ContentType)?>((stream, contentType));
    }

    public Task DeleteAsync(string reference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Resolve(reference);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string Resolve(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference) || !reference.StartsWith("transfer-evidence/", StringComparison.Ordinal))
            throw new ArgumentException("Invalid evidence reference.", nameof(reference));
        var relative = reference["transfer-evidence/".Length..];
        if (relative.Contains('/') || relative.Contains('\\') || relative.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("Invalid evidence reference.", nameof(reference));
        var candidate = Path.GetFullPath(Path.Combine(_root, relative));
        if (!candidate.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Invalid evidence reference.", nameof(reference));
        return candidate;
    }

    private static bool HasExpectedSignature(string extension, byte[] content)
    {
        return extension.ToLowerInvariant() switch
        {
            ".pdf" => content.Length >= 5 && content.AsSpan(0, 5).SequenceEqual("%PDF-"u8),
            ".jpg" or ".jpeg" => content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF,
            ".png" => content.Length >= 8 && content.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            _ => false
        };
    }
}
