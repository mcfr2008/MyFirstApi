using System.Security.Cryptography;
using MyFirstApi.Interfaces;

namespace MyFirstApi.Services;

// Stores files on the local disk under FileStorage:RootPath (relative to the app's
// content root; "App_Data/files" by default). In Docker this folder is a volume.
public class LocalFileStorage : IFileStorage
{
    private readonly string _rootPath;

    public LocalFileStorage(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configured = configuration["FileStorage:RootPath"] ?? Path.Combine("App_Data", "files");
        _rootPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, configured));
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<StoredContent> SaveAsync(Stream content, string extension, CancellationToken cancellationToken = default)
    {
        // yyyy/MM/<guid>.<ext>: keeps folders small and keys unguessable.
        var now = DateTime.UtcNow;
        var key = $"{now:yyyy}/{now:MM}/{Guid.NewGuid():N}.{extension}";
        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var sha256 = SHA256.Create();
        await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
        await using (var hashing = new CryptoStream(file, sha256, CryptoStreamMode.Write))
        {
            await content.CopyToAsync(hashing, cancellationToken);
        }

        return new StoredContent(key, new FileInfo(path).Length, Convert.ToHexStringLower(sha256.Hash!));
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(storageKey);
        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(storageKey);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        return Task.CompletedTask;
    }

    // Guards against keys escaping the root folder (e.g. "../").
    private string ResolvePath(string storageKey)
    {
        var path = Path.GetFullPath(Path.Combine(_rootPath, storageKey));
        if (!path.StartsWith(_rootPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid storage key.");
        }
        return path;
    }
}
