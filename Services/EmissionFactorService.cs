using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MyFirstApi.Data;
using MyFirstApi.Dtos;
using MyFirstApi.Exceptions;
using MyFirstApi.Interfaces;
using MyFirstApi.Models;

namespace MyFirstApi.Services;

public class EmissionFactorService
    : MasterDataService<EmissionFactor, EmissionFactorRequest, EmissionFactorResponse, EmissionFactorQuery>, IEmissionFactorService
{
    public EmissionFactorService(AppDbContext context) : base(context)
    {
    }

    protected override DbSet<EmissionFactor> Set => Context.EmissionFactors;

    protected override string EntityName => "EmissionFactor";

    protected override Expression<Func<EmissionFactor, EmissionFactorResponse>> Projection => f => new EmissionFactorResponse
    {
        Id = f.Id,
        Code = f.Code,
        Name = f.Name,
        Mode = f.Mode,
        Carrier = f.Carrier == null ? null : new ReferenceSummary(f.Carrier.Id, f.Carrier.Code, f.Carrier.Name),
        GramsCo2ePerTonneKm = f.GramsCo2ePerTonneKm,
        Source = f.Source,
        IsActive = f.IsActive,
        CreatedAt = f.CreatedAt,
        UpdatedAt = f.UpdatedAt
    };

    protected override Expression<Func<EmissionFactor, bool>> MatchesSearch(string pattern) =>
        f => EF.Functions.ILike(f.Code, pattern) ||
             EF.Functions.ILike(f.Name, pattern);

    protected override IQueryable<EmissionFactor> ApplyFilters(IQueryable<EmissionFactor> items, EmissionFactorQuery query)
    {
        if (query.Mode.HasValue)
        {
            items = items.Where(f => f.Mode == query.Mode.Value);
        }
        if (query.CarrierId.HasValue)
        {
            items = items.Where(f => f.CarrierId == query.CarrierId);
        }
        return items;
    }

    protected override IOrderedQueryable<EmissionFactor> ApplyOrder(IQueryable<EmissionFactor> items) =>
        items.OrderBy(f => f.Mode).ThenBy(f => f.CarrierId != null).ThenBy(f => f.Code);

    protected override async Task ValidateAsync(EmissionFactor entity, EmissionFactorRequest request)
    {
        var mode = request.Mode!.Value;
        var carrier = await ReferenceResolver.ResolveAsync(
            Context.Carriers.AsNoTracking(), request.CarrierId, entity.CarrierId, "carrierId");
        if (carrier != null && !carrier.Modes.Any(m => TransportModes.SameFamily(Enum.Parse<TransportMode>(m), mode)))
        {
            throw Errors.CarrierModeNotSupported(carrier.Code, mode);
        }

        // One factor per mode default, and one per (mode, carrier).
        var existing = await Set.AsNoTracking()
            .Where(f => f.Id != entity.Id && f.Mode == mode && f.CarrierId == request.CarrierId)
            .Select(f => f.Code)
            .FirstOrDefaultAsync();
        if (existing != null)
        {
            throw Errors.EmissionFactorOverlap(mode, carrier?.Code, existing);
        }
    }

    protected override void Apply(EmissionFactor entity, EmissionFactorRequest request)
    {
        entity.Name = request.Name.Trim();
        entity.Mode = request.Mode!.Value;
        entity.CarrierId = request.CarrierId;
        entity.GramsCo2ePerTonneKm = request.GramsCo2ePerTonneKm!.Value;
        entity.Source = QueryHelpers.NullIfBlank(request.Source);
    }
}
