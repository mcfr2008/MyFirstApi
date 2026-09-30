using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MyFirstApi.Data;
using MyFirstApi.Dtos;
using MyFirstApi.Interfaces;
using MyFirstApi.Models;

namespace MyFirstApi.Services;

public class ItemCategoryService
    : MasterDataService<ItemCategory, ItemCategoryRequest, ItemCategoryResponse, MasterDataQuery>, IItemCategoryService
{
    public ItemCategoryService(AppDbContext context) : base(context)
    {
    }

    protected override DbSet<ItemCategory> Set => Context.ItemCategories;

    protected override string EntityName => "Item category";

    protected override Expression<Func<ItemCategory, ItemCategoryResponse>> Projection => c => new ItemCategoryResponse
    {
        Id = c.Id,
        Code = c.Code,
        Name = c.Name,
        Description = c.Description,
        RequiredAttributes = c.RequiredAttributes,
        IsFragile = c.IsFragile,
        RequiresTemperatureControl = c.RequiresTemperatureControl,
        IsDangerousGoods = c.IsDangerousGoods,
        UnNumber = c.UnNumber,
        HazardClass = c.HazardClass,
        IsActive = c.IsActive,
        ItemCount = Context.TrackedItems.Count(i => i.CategoryId == c.Id && !i.IsArchived),
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
    };

    protected override Expression<Func<ItemCategory, bool>> MatchesSearch(string pattern) =>
        c => EF.Functions.ILike(c.Code, pattern) || EF.Functions.ILike(c.Name, pattern);

    protected override IOrderedQueryable<ItemCategory> ApplyOrder(IQueryable<ItemCategory> items) =>
        items.OrderBy(c => c.Name);

    protected override void Apply(ItemCategory entity, ItemCategoryRequest request)
    {
        entity.Name = request.Name.Trim();
        entity.Description = QueryHelpers.NullIfBlank(request.Description);
        entity.RequiredAttributes = (request.RequiredAttributes ?? [])
            .Select(k => k.Trim())
            .Distinct()
            .ToList();
        entity.IsFragile = request.IsFragile;
        entity.RequiresTemperatureControl = request.RequiresTemperatureControl;
        entity.IsDangerousGoods = request.IsDangerousGoods;
        entity.UnNumber = request.IsDangerousGoods ? request.UnNumber!.Trim().ToUpperInvariant() : null;
        entity.HazardClass = request.IsDangerousGoods ? request.HazardClass!.Trim() : null;
    }
}
