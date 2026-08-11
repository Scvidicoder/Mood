using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.DTOs.Orders;

namespace MoodPickup.Api.Interfaces;

public interface IOrderWorkflowService
{
    Task<PagedResponse<KitchenOrderDto>> GetKitchenOrdersAsync(
        KitchenOrderListQuery query,
        CancellationToken cancellationToken);

    Task<KitchenOrderDto> StartPreparationAsync(
        Guid id,
        OrderVersionRequest request,
        CancellationToken cancellationToken);

    Task<KitchenOrderDto> MarkReadyAsync(
        Guid id,
        OrderVersionRequest request,
        CancellationToken cancellationToken);

    Task<KitchenOrderDto> UpdateKitchenEtaAsync(
        Guid id,
        UpdateEstimatedReadyTimeRequest request,
        CancellationToken cancellationToken);

    Task<StaffOrderDetailDto> RecordPaymentAsync(
        Guid id,
        RecordPaymentRequest request,
        CancellationToken cancellationToken);

    Task<StaffOrderDetailDto> CompleteAsync(
        Guid id,
        OrderVersionRequest request,
        CancellationToken cancellationToken);
}
