using Microsoft.AspNetCore.Mvc;
using MyFirstApi.Dtos;
using MyFirstApi.Interfaces;

namespace MyFirstApi.Controllers;

[Route("api/v{version:apiVersion}/[controller]")]
public class ItemCategoriesController : MasterDataController<ItemCategoryRequest, ItemCategoryResponse, MasterDataQuery>
{
    public ItemCategoriesController(IItemCategoryService service) : base(service)
    {
    }
}
