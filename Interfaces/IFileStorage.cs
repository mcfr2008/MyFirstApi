namespace MyFirstApi.Interfaces;

// Where uploaded file bytes live. LocalFileStorage (disk / Docker volume) for now;
// a cloud implementation (S3, MinIO, Azure Blob) can replace it without touching callers.
public interface IFileStorage
{
    // Writes the stream under a new unique key; returns the key, size and SHA-256.
    Task<StoredContent> SaveAsync(Stream content, string extension, CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);

    // Best-effort cleanup (e.g. after the database save failed).
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}

public record StoredContent(string StorageKey, long SizeBytes, string Sha256);
