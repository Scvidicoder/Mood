using MoodPickup.Api.DTOs;

namespace MoodPickup.Api.Interfaces;

public interface ICustomerProfileService
{
    Task<CustomerProfileDto> GetAsync(CancellationToken cancellationToken);

    Task<CustomerProfileDto> UpdateAsync(
        UpdateCustomerProfileRequest request,
        CancellationToken cancellationToken);
}
