using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MyFirstApi.Data;
using MyFirstApi.Interfaces;
using MyFirstApi.Models;

namespace MyFirstApi.Services;

public class MasterDataCache : IMasterDataCache
{
    private const string EventTypesKey = "master-data:event-types";
    private const string ReasonCodesKey = "master-data:reason-codes";

    // Safety net when several API instances run: a change made through another
    // instance (which only clears its own cache) shows up here within this time.
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;

    public MasterDataCache(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<EventType?> FindEventTypeAsync(string code) =>
        (await GetEventTypesAsync()).FirstOrDefault(e => e.Code == code);

    public async Task<EventType?> FindEventTypeAsync(int id) =>
        (await GetEventTypesAsync()).FirstOrDefault(e => e.Id == id);

    public async Task<ReasonCode?> FindReasonCodeAsync(string code) =>
        (await _cache.GetOrCreateAsync(ReasonCodesKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Lifetime;
            return _context.ReasonCodes.AsNoTracking().ToListAsync();
        }))!.FirstOrDefault(r => r.Code == code);

    public void Invalidate()
    {
        _cache.Remove(EventTypesKey);
        _cache.Remove(ReasonCodesKey);
    }

    private async Task<List<EventType>> GetEventTypesAsync() =>
        (await _cache.GetOrCreateAsync(EventTypesKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Lifetime;
            return _context.EventTypes.AsNoTracking().ToListAsync();
        }))!;
}
