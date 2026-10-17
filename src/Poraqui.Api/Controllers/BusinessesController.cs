using System.Data;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Poraqui.Application.Commerce;
using Poraqui.Application.Security;
using Poraqui.Infrastructure.Identity;
using Poraqui.Infrastructure.Persistence;

namespace Poraqui.Api.Controllers;

[ApiController]
[Route("api/businesses")]
public sealed class BusinessesController(
    BusinessService businesses,
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole<Guid>> roles,
    AppDbContext db) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("active")]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var result = await businesses.GetActiveAsync(cancellationToken);
        return Ok(result.Select(item => new PublicBusiness(item.Id, item.Name, item.Address)));
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Register(RegisterBusinessRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(users.GetUserId(User), out var ownerId)) return Unauthorized();
        var owner = await users.FindByIdAsync(ownerId.ToString());
        if (owner is null) return Unauthorized();

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (!await roles.RoleExistsAsync(ApplicationRoles.BusinessOwner))
        {
            var roleResult = await roles.CreateAsync(new IdentityRole<Guid>(ApplicationRoles.BusinessOwner));
            if (!roleResult.Succeeded) return Problem("Business owner role could not be initialized.");
        }
        if (!await users.IsInRoleAsync(owner, ApplicationRoles.BusinessOwner))
        {
            var assignment = await users.AddToRoleAsync(owner, ApplicationRoles.BusinessOwner);
            if (!assignment.Succeeded) return Problem("Business owner access could not be assigned.");
        }

        var result = await businesses.RegisterAsync(ownerId, request.Name, request.Address, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { businessId = result.Id }, result);
    }

    [Authorize(Policy = ApplicationPolicies.ManageBusiness)]
    [HttpGet("{businessId:guid}")]
    public async Task<IActionResult> GetById(Guid businessId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var isAdmin)) return Unauthorized();
        try
        {
            var result = await businesses.GetAsync(businessId, actorId, isAdmin, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (BusinessAuthorizationException) { return Forbid(); }
    }

    [Authorize(Policy = ApplicationPolicies.ManageBusiness)]
    [HttpPut("{businessId:guid}")]
    public async Task<IActionResult> Configure(Guid businessId, ConfigureBusinessRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var isAdmin)) return Unauthorized();
        try
        {
            var result = await businesses.ConfigureAsync(businessId, actorId, isAdmin, request.Name, request.Address, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (BusinessAuthorizationException) { return Forbid(); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [Authorize(Policy = ApplicationPolicies.ManageBusiness)]
    [HttpGet("{businessId:guid}/transfer-details")]
    public async Task<IActionResult> GetTransferDetails(Guid businessId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var isAdmin)) return Unauthorized();
        try
        {
            var result = await businesses.GetTransferDetailsAsync(businessId, actorId, isAdmin, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (BusinessAuthorizationException) { return Forbid(); }
    }

    [Authorize(Policy = ApplicationPolicies.ManageBusiness)]
    [HttpPut("{businessId:guid}/transfer-details")]
    public async Task<IActionResult> ConfigureTransferDetails(Guid businessId, ConfigureTransferDetailsRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var isAdmin)) return Unauthorized();
        try
        {
            var result = await businesses.ConfigureTransferDetailsAsync(businessId, actorId, isAdmin,
                request.RecipientName, request.Institution, request.AccountReference, request.Instructions, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (BusinessAuthorizationException) { return Forbid(); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [Authorize(Policy = ApplicationPolicies.PlatformAdministration)]
    [HttpPut("{businessId:guid}/active")]
    public async Task<IActionResult> SetActive(Guid businessId, SetBusinessActiveRequest request, CancellationToken cancellationToken)
    {
        var result = await businesses.SetActiveAsync(businessId, request.Active, isPlatformAdmin: true, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    private bool TryGetActor(out Guid actorId, out bool isAdmin)
    {
        isAdmin = User.IsInRole(ApplicationRoles.PlatformAdmin);
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? users.GetUserId(User), out actorId);
    }
}

public sealed record RegisterBusinessRequest(string Name, string Address);
public sealed record ConfigureBusinessRequest(string Name, string Address);
public sealed record SetBusinessActiveRequest(bool Active);
public sealed record PublicBusiness(Guid Id, string Name, string Address);
public sealed record ConfigureTransferDetailsRequest(
    [Required, StringLength(160, MinimumLength = 2)] string RecipientName,
    [Required, StringLength(120, MinimumLength = 2)] string Institution,
    [Required, StringLength(120, MinimumLength = 4)] string AccountReference,
    [StringLength(500)] string? Instructions);
