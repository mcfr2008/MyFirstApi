using MyFirstApi.Dtos;

namespace MyFirstApi.Interfaces;

public interface IEventTypeService : IMasterDataService<EventTypeRequest, EventTypeResponse, MasterDataQuery>
{
}
