using MyFirstApi.Dtos;

namespace MyFirstApi.Interfaces;

public interface IReturnRequestService
{
    Task<PagedResult<ReturnRequestResponse>> GetAllAsync(ReturnRequestQuery query);
    Task<ReturnRequestResponse?> GetByIdAsync(int id);
    Task<ReturnRequestResponse?> GetByRmaNumberAsync(string rmaNumber);
    Task<ReturnRequestResponse> CreateAsync(CreateReturnRequestRequest request);
    // Creates the return shipment (receiver -> sender), optionally auto-routed.
    Task<ApproveReturnRequestResponse?> ApproveAsync(int id, ApproveReturnRequestRequest request);
    Task<ReturnRequestResponse?> RejectAsync(int id, DecideReturnRequestRequest request);
    Task<ReturnRequestResponse?> CancelAsync(int id, DecideReturnRequestRequest request);
}
