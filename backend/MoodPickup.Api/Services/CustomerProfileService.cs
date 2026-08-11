using Microsoft.EntityFrameworkCore;
using MoodPickup.Api.Data;
using MoodPickup.Api.DTOs;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Services;

public sealed class CustomerProfileService(
    MoodPickupDbContext dbContext,
    ICurrentUserContext currentUser) : ICustomerProfileService
{
    private static readonly OrderStatus[] ActiveStatuses =
    [
        OrderStatus.PendingConfirmation,
        OrderStatus.Confirmed,
        OrderStatus.Preparing,
        OrderStatus.ReadyForPickup
    ];

    public async Task<CustomerProfileDto> GetAsync(
        CancellationToken cancellationToken)
    {
        var customerId = currentUser.GetRequiredCustomerId();
        var customer = await dbContext.Customers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == customerId,
                cancellationToken)
            ?? throw Unauthorized();

        return await ToDtoAsync(customer, cancellationToken);
    }

    public async Task<CustomerProfileDto> UpdateAsync(
        UpdateCustomerProfileRequest request,
        CancellationToken cancellationToken)
    {
        var customerId = currentUser.GetRequiredCustomerId();
        var customer = await dbContext.Customers.SingleOrDefaultAsync(
            candidate => candidate.Id == customerId,
            cancellationToken)
            ?? throw Unauthorized();

        EnsureVersion(request.RowVersion, customer.RowVersion);
        customer.Name = request.Name.Trim();

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw VersionConflict();
        }

        return await ToDtoAsync(customer, cancellationToken);
    }

    private async Task<CustomerProfileDto> ToDtoAsync(
        Customer customer,
        CancellationToken cancellationToken)
    {
        var activeOrderCount = await dbContext.Orders.CountAsync(
            order => order.CustomerId == customer.Id &&
                     ActiveStatuses.Contains(order.Status),
            cancellationToken);
        var completedOrderCount = await dbContext.Orders.CountAsync(
            order => order.CustomerId == customer.Id &&
                     order.Status == OrderStatus.Completed,
            cancellationToken);

        return new CustomerProfileDto(
            customer.Name,
            customer.PhoneNumber,
            true,
            customer.TelegramChatId is not null,
            customer.CreatedAt,
            activeOrderCount,
            completedOrderCount,
            customer.RowVersion);
    }

    private static void EnsureVersion(Guid expected, Guid current)
    {
        if (expected != current)
        {
            throw VersionConflict();
        }
    }

    private static ApiProblemException VersionConflict()
    {
        return new ApiProblemException(
            StatusCodes.Status409Conflict,
            "concurrency_conflict",
            "The profile was changed in another session",
            "PROFILE_VERSION_CONFLICT",
            "Refresh the profile and try again.");
    }

    private static ApiProblemException Unauthorized()
    {
        return new ApiProblemException(
            StatusCodes.Status401Unauthorized,
            "unauthorized",
            "Authentication required");
    }
}
