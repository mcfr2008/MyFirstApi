using MyFirstApi.Dtos;

namespace MyFirstApi.Interfaces;

public interface ILaneService : IMasterDataService<LaneRequest, LaneResponse, LaneQuery>
{
}
