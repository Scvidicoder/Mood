using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.DTOs.Orders;

namespace MoodPickup.Api.Interfaces;

public interface IOrderService
{
    Task<OrderDetailDto> CreateAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken);

    Task<OrderDetailDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<PagedResponse<OrderSummaryDto>> GetMineAsync(
        OrderListQuery query,
        CancellationToken cancellationToken);

    Task<OrderDetailDto> CancelAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<RepeatOrderResultDto> RepeatAsync(
        Guid id,
        CancellationToken cancellationToken);
}
