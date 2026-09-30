using System.Text.Json.Serialization;

namespace MyFirstApi.Models;

[JsonConverter(typeof(JsonStringEnumConverter<FilePurpose>))]
public enum FilePurpose
{
    Signature,
    DeliveryPhoto,
    Document
}

// Metadata of an uploaded file; the bytes live in IFileStorage under StorageKey.
public class StoredFile
{
    // Random, so download URLs can't be guessed.
    public Guid Id { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string? OriginalFileName { get; set; }
    // Detected from the file's content, not trusted from the client.
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    // Hex SHA-256 of the content: evidence the file wasn't changed after upload.
    public string Sha256 { get; set; } = string.Empty;
    public FilePurpose Purpose { get; set; }
    public DateTime UploadedAt { get; set; }
    public string? UploadedBy { get; set; }
}
