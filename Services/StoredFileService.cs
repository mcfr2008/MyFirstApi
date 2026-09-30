using Microsoft.EntityFrameworkCore;
using MyFirstApi.Data;
using MyFirstApi.Interfaces;
using MyFirstApi.Models;

namespace MyFirstApi.Services;

public class StoredFileService : IStoredFileService
{
    private readonly AppDbContext _context;
    private readonly IFileStorage _fileStorage;

    public StoredFileService(AppDbContext context, IFileStorage fileStorage)
    {
        _context = context;
        _fileStorage = fileStorage;
    }

    public async Task<(StoredFile File, Stream Content)?> OpenAsync(Guid id)
    {
        var file = await _context.StoredFiles.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
        if (file == null) return null;

        var content = await _fileStorage.OpenReadAsync(file.StorageKey);
        return content == null ? null : (file, content);
    }
}
