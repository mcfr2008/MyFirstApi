using MyFirstApi.Dtos;

namespace MyFirstApi.Interfaces;

public interface IItemCategoryService : IMasterDataService<ItemCategoryRequest, ItemCategoryResponse, MasterDataQuery>
{
}
