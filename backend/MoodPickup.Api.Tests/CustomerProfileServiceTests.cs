using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MoodPickup.Api.Data;
using MoodPickup.Api.DTOs;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;
using MoodPickup.Api.Services;
using MoodPickup.Api.Validators;

namespace MoodPickup.Api.Tests;

public sealed class CustomerProfileServiceTests
{
    [Fact]
    public async Task GetAndUpdate_ReturnSafeProfileCountsAndNewVersion()
    {
        await using var dbContext = CreateDbContext();
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Original Name",
            PhoneNumber = "+992900000001",
            TelegramChatId = 123456
        };
        dbContext.Customers.Add(customer);
        dbContext.Orders.AddRange(
            Order(customer, OrderStatus.Confirmed, "MP-1"),
            Order(customer, OrderStatus.Preparing, "MP-2"),
            Order(customer, OrderStatus.Completed, "MP-3"),
            Order(customer, OrderStatus.Rejected, "MP-4"));
        await dbContext.SaveChangesAsync();
        var service = new CustomerProfileService(
            dbContext,
            new CustomerContext(customer.Id));

        var profile = await service.GetAsync(CancellationToken.None);
        Assert.Equal(2, profile.ActiveOrderCount);
        Assert.Equal(1, profile.CompletedOrderCount);
        Assert.True(profile.PhoneVerified);
        Assert.True(profile.TelegramLinked);

        var updated = await service.UpdateAsync(
            new UpdateCustomerProfileRequest("  Updated Name  ", profile.RowVersion),
            CancellationToken.None);

        Assert.Equal("Updated Name", updated.Name);
        Assert.NotEqual(profile.RowVersion, updated.RowVersion);
        Assert.Equal("+992900000001", updated.PhoneNumber);
    }

    [Fact]
    public async Task Update_WithStaleVersion_ReturnsProfileConflict()
    {
        await using var dbContext = CreateDbContext();
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Original Name",
            PhoneNumber = "+992900000002"
        };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();
        var staleVersion = customer.RowVersion;
        customer.Name = "Changed elsewhere";
        await dbContext.SaveChangesAsync();
        var service = new CustomerProfileService(
            dbContext,
            new CustomerContext(customer.Id));

        var exception = await Assert.ThrowsAsync<ApiProblemException>(() =>
            service.UpdateAsync(
                new UpdateCustomerProfileRequest("Another name", staleVersion),
                CancellationToken.None));

        Assert.Equal(StatusCodes.Status409Conflict, exception.Status);
        Assert.Equal("PROFILE_VERSION_CONFLICT", exception.Code);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("A")]
    [InlineData(" A ")]
    public void Validator_RejectsEmptyOrTooShortTrimmedNames(string name)
    {
        var result = new UpdateCustomerProfileRequestValidator().Validate(
            new UpdateCustomerProfileRequest(name, Guid.NewGuid()));

        Assert.False(result.IsValid);
    }

    private static MoodPickupDbContext CreateDbContext()
    {
        return new MoodPickupDbContext(
            new DbContextOptionsBuilder<MoodPickupDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
    }

    private static Order Order(
        Customer customer,
        OrderStatus status,
        string number)
    {
        return new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Customer = customer,
            OrderNumber = number,
            CustomerName = customer.Name,
            CustomerPhoneNumber = customer.PhoneNumber,
            Currency = "TJS",
            Status = status
        };
    }

    private sealed class CustomerContext(Guid customerId) : ICurrentUserContext
    {
        public string CorrelationId => "profile-test";

        public Guid GetRequiredCustomerId() => customerId;

        public Guid GetRequiredEmployeeId() => throw new NotSupportedException();
    }
}
