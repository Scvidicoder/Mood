using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MoodPickup.Api.DTOs;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Services;

namespace MoodPickup.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/auth/customer")]
public sealed class CustomerAuthController(
    CustomerAuthenticationService authenticationService,
    AuthenticationCookieService cookieService) : ControllerBase
{
    [HttpPost("request-code")]
    [EnableRateLimiting("customer-code-request")]
    [ProducesResponseType<RequestCustomerCodeResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RequestCustomerCodeResponse>> RequestCode(
        RequestCustomerCodeRequest request,
        CancellationToken cancellationToken)
    {
        var response = await authenticationService.RequestCodeAsync(
            request,
            AuthenticationRequestMetadata.FromHttpContext(HttpContext),
            cancellationToken);

        return Ok(response);
    }

    [HttpPost("challenge-status")]
    [EnableRateLimiting("challenge-status")]
    [ProducesResponseType<CustomerChallengeStatusResponse>(
        StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerChallengeStatusResponse>>
        ChallengeStatus(
            CustomerChallengeStatusRequest request,
            CancellationToken cancellationToken)
    {
        return Ok(await authenticationService.GetChallengeStatusAsync(
            request,
            cancellationToken));
    }

    [HttpPost("verify-code")]
    [ProducesResponseType<CustomerVerificationResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerVerificationResponse>> VerifyCode(
        VerifyCustomerCodeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.VerifyCodeAsync(
            request,
            AuthenticationRequestMetadata.FromHttpContext(HttpContext),
            cancellationToken);

        if (result.IsNewCustomer)
        {
            return Ok(new CustomerVerificationResponse(
                true,
                null,
                null,
                null,
                result.RegistrationToken));
        }

        cookieService.SetSessionCookies(
            Response,
            result.RefreshToken!.RawToken,
            result.RefreshToken.ExpiresAt);

        return Ok(new CustomerVerificationResponse(
            false,
            result.AccessToken!.Value,
            result.AccessToken.ExpiresInSeconds,
            new CustomerSummary(
                result.Customer!.Id,
                result.Customer.Name,
                result.Customer.PhoneNumber),
            null));
    }

    [HttpPost("complete-registration")]
    [ProducesResponseType<CustomerAuthenticationResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerAuthenticationResponse>> CompleteRegistration(
        CompleteCustomerRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.CompleteRegistrationAsync(
            request,
            AuthenticationRequestMetadata.FromHttpContext(HttpContext),
            cancellationToken);

        cookieService.SetSessionCookies(
            Response,
            result.RefreshToken!.RawToken,
            result.RefreshToken.ExpiresAt);

        return Ok(new CustomerAuthenticationResponse(
            result.AccessToken!.Value,
            result.AccessToken.ExpiresInSeconds,
            new CustomerSummary(
                result.Customer!.Id,
                result.Customer.Name,
                result.Customer.PhoneNumber)));
    }
}
