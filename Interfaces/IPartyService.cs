using MyFirstApi.Dtos;

namespace MyFirstApi.Interfaces;

public interface IPartyService : IMasterDataService<PartyRequest, PartyResponse, PartyQuery>
{
}
