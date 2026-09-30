using MyFirstApi.Models;

namespace MyFirstApi.Interfaces;

public interface IStoredFileService
{
    // Metadata + an open stream of the content, or null when the file doesn't exist.
    Task<(StoredFile File, Stream Content)?> OpenAsync(Guid id);
}
