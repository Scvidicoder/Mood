using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.DTOs.Orders;

namespace MoodPickup.Api.Interfaces;

public interface IStaffOrderService
{
    Task<PagedResponse<StaffOrderSummaryDto>> GetAsync(
        StaffOrderListQuery query,
        CancellationToken cancellationToken);

    Task<StaffOrderDetailDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<PagedResponse<StaffOrderSummaryDto>> GetKitchenOrdersAsync(
        PaginationQuery query,
        CancellationToken cancellationToken);

    Task<StaffOrderDetailDto> ConfirmAsync(
        Guid id,
        ConfirmOrderRequest request,
        CancellationToken cancellationToken);

    Task<StaffOrderDetailDto> RejectAsync(
        Guid id,
        RejectOrderRequest request,
        CancellationToken cancellationToken);

    Task<StaffOrderDetailDto> UpdateEstimatedReadyTimeAsync(
        Guid id,
        UpdateEstimatedReadyTimeRequest request,
        CancellationToken cancellationToken);
}
