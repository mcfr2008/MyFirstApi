using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MyFirstApi.Data;
using MyFirstApi.Dtos;
using MyFirstApi.Exceptions;
using MyFirstApi.Interfaces;
using MyFirstApi.Models;

namespace MyFirstApi.Services;

public class VehicleService
    : MasterDataService<Vehicle, VehicleRequest, VehicleResponse, VehicleQuery>, IVehicleService
{
    public VehicleService(AppDbContext context) : base(context)
    {
    }

    protected override DbSet<Vehicle> Set => Context.Vehicles;

    protected override string EntityName => "Vehicle";

    protected override Expression<Func<Vehicle, VehicleResponse>> Projection => v => new VehicleResponse
    {
        Id = v.Id,
        Code = v.Code,
        Name = v.Name,
        Mode = v.Mode,
        Carrier = v.Carrier == null ? null : new ReferenceSummary(v.Carrier.Id, v.Carrier.Code, v.Carrier.Name),
        RegistrationNumber = v.RegistrationNumber,
        ImoNumber = v.ImoNumber,
        CapacityKg = v.CapacityKg,
        IsActive = v.IsActive,
        CreatedAt = v.CreatedAt,
        UpdatedAt = v.UpdatedAt
    };

    protected override Expression<Func<Vehicle, bool>> MatchesSearch(string pattern) =>
        v => EF.Functions.ILike(v.Code, pattern) ||
             EF.Functions.ILike(v.Name, pattern) ||
             (v.RegistrationNumber != null && EF.Functions.ILike(v.RegistrationNumber, pattern)) ||
             (v.ImoNumber != null && EF.Functions.ILike(v.ImoNumber, pattern));

    protected override IQueryable<Vehicle> ApplyFilters(IQueryable<Vehicle> items, VehicleQuery query)
    {
        if (query.Mode.HasValue)
        {
            items = items.Where(v => v.Mode == query.Mode.Value);
        }
        if (query.CarrierId.HasValue)
        {
            items = items.Where(v => v.CarrierId == query.CarrierId);
        }
        return items;
    }

    protected override IOrderedQueryable<Vehicle> ApplyOrder(IQueryable<Vehicle> items) =>
        items.OrderBy(v => v.Name);

    protected override async Task ValidateAsync(Vehicle entity, VehicleRequest request)
    {
        var carrier = await ReferenceResolver.ResolveAsync(
            Context.Carriers.AsNoTracking(), request.CarrierId, entity.CarrierId, "Carrier");

        var mode = request.Mode!.Value;
        if (carrier != null && !carrier.Modes.Contains(mode.ToString()))
        {
            throw new BusinessRuleException($"Carrier {carrier.Code} does not operate {mode} transport.");
        }
        if (request.ImoNumber != null && mode != TransportMode.Sea)
        {
            throw new BusinessRuleException("ImoNumber applies to Sea vehicles (vessels) only.");
        }
    }

    protected override void Apply(Vehicle entity, VehicleRequest request)
    {
        entity.Name = request.Name.Trim();
        entity.Mode = request.Mode!.Value;
        entity.CarrierId = request.CarrierId;
        entity.RegistrationNumber = QueryHelpers.NullIfBlank(request.RegistrationNumber)?.ToUpperInvariant();
        entity.ImoNumber = QueryHelpers.NullIfBlank(request.ImoNumber);
        entity.CapacityKg = request.CapacityKg;
    }
}
