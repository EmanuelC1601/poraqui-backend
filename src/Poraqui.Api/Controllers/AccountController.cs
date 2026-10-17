using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Poraqui.Infrastructure.Identity;

namespace Poraqui.Api.Controllers;

[ApiController]
[Route("api/account")]
[Authorize]
public sealed class AccountController(UserManager<ApplicationUser> users) : ControllerBase
{
    [HttpPut("phone")]
    public async Task<IActionResult> SetPhoneNumber(SetPhoneNumberRequest request)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        var result = await users.SetPhoneNumberAsync(user, request.PhoneNumber.Trim());
        return result.Succeeded ? NoContent() : BadRequest(new { errors = result.Errors.Select(error => error.Description) });
    }
}

public sealed record SetPhoneNumberRequest([Required, Phone, StringLength(24, MinimumLength = 7)] string PhoneNumber);
