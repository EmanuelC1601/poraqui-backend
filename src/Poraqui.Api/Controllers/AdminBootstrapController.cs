using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Poraqui.Application.Security;
using Poraqui.Infrastructure.Identity;
using Poraqui.Infrastructure.Persistence;

namespace Poraqui.Api.Controllers;

[ApiController]
[Route("api/auth/bootstrap-admin")]
public sealed class AdminBootstrapController(
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole<Guid>> roles,
    AppDbContext db,
    IConfiguration configuration) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> CreateFirstAdministrator([FromBody] BootstrapAdminRequest request, CancellationToken cancellationToken)
    {
        var configuredKey = configuration["Bootstrap:AdminKey"];
        if (string.IsNullOrWhiteSpace(configuredKey)) return NotFound();
        if (!SecureEquals(configuredKey, request.BootstrapKey)) return Unauthorized();

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var administrators = await users.GetUsersInRoleAsync(ApplicationRoles.PlatformAdmin);
        if (administrators.Count != 0) return Conflict(new { error = "Administrator bootstrap is already complete." });

        var email = request.Email.Trim();
        if (await users.FindByEmailAsync(email) is not null)
            return Conflict(new { error = "An account already exists for this email." });

        if (!await roles.RoleExistsAsync(ApplicationRoles.PlatformAdmin))
        {
            var roleResult = await roles.CreateAsync(new IdentityRole<Guid>(ApplicationRoles.PlatformAdmin));
            if (!roleResult.Succeeded) return Problem("Administrator role could not be initialized.");
        }

        var user = new ApplicationUser { UserName = email, Email = email, DisplayName = request.DisplayName.Trim() };
        var createResult = await users.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
            return BadRequest(new { errors = createResult.Errors.Select(error => error.Description) });

        var addRoleResult = await users.AddToRoleAsync(user, ApplicationRoles.PlatformAdmin);
        if (!addRoleResult.Succeeded)
        {
            return Problem("Administrator role could not be assigned.");
        }

        await transaction.CommitAsync(cancellationToken);
        return Created("/api/auth/login", new { user.Id, user.Email });
    }

    private static bool SecureEquals(string expected, string? supplied)
    {
        if (string.IsNullOrEmpty(supplied)) return false;
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        return expectedBytes.Length == suppliedBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }
}

public sealed record BootstrapAdminRequest(
    [property: Required, EmailAddress, StringLength(256)] string Email,
    [property: Required, StringLength(120, MinimumLength = 2)] string DisplayName,
    [property: Required, StringLength(256, MinimumLength = 12)] string Password,
    [property: Required, StringLength(256, MinimumLength = 32)] string BootstrapKey);
