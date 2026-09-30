using MyFirstApi.Models;

namespace MyFirstApi.Interfaces;

// In-memory lookups for small, rarely-changing master data that is read on every
// tracking event (event types, reason codes). Returned entities are detached
// (not tracked by the DbContext): read their values, assign ids, never attach them.
public interface IMasterDataCache
{
    Task<EventType?> FindEventTypeAsync(string code);
    Task<EventType?> FindEventTypeAsync(int id);
    Task<ReasonCode?> FindReasonCodeAsync(string code);

    // Called after event types / reason codes are saved.
    void Invalidate();
}
