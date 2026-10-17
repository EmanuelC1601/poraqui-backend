using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Poraqui.Application.Accounts;
using Poraqui.Infrastructure.Identity;

namespace Poraqui.Api.Controllers;

[ApiController]
[Route("api/account")]
[Authorize]
public sealed class AccountController(UserManager<ApplicationUser> users, IPhoneVerificationProvider phoneVerification) : ControllerBase
{
    [HttpPost("phone/verification")]
    [EnableRateLimiting("PhoneVerification")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> StartPhoneVerification(SetPhoneNumberRequest request, CancellationToken cancellationToken)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        var phoneNumber = request.PhoneNumber.Trim();
        try
        {
            await phoneVerification.StartAsync(phoneNumber, cancellationToken);
            return Accepted();
        }
        catch (PhoneVerificationProviderException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Phone verification is temporarily unavailable." });
        }
    }

    [HttpPost("phone/verification/confirm")]
    [EnableRateLimiting("PhoneVerification")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ConfirmPhoneVerification(ConfirmPhoneNumberRequest request, CancellationToken cancellationToken)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        var phoneNumber = request.PhoneNumber.Trim();
        try
        {
            if (!await phoneVerification.IsApprovedAsync(phoneNumber, request.Code, cancellationToken))
                return BadRequest(new { error = "Phone verification code is invalid or expired." });
        }
        catch (PhoneVerificationProviderException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Phone verification is temporarily unavailable." });
        }

        var setResult = await users.SetPhoneNumberAsync(user, phoneNumber);
        if (!setResult.Succeeded)
            return BadRequest(new { errors = setResult.Errors.Select(error => error.Description) });
        user.PhoneNumberConfirmed = true;
        var updateResult = await users.UpdateAsync(user);
        return updateResult.Succeeded
            ? NoContent()
            : BadRequest(new { errors = updateResult.Errors.Select(error => error.Description) });
    }
}

public sealed record SetPhoneNumberRequest([Required, RegularExpression(@"^\+[1-9]\d{7,14}$")] string PhoneNumber);
public sealed record ConfirmPhoneNumberRequest([Required, RegularExpression(@"^\+[1-9]\d{7,14}$")] string PhoneNumber,
    [Required, RegularExpression(@"^\d{4,10}$")] string Code);
