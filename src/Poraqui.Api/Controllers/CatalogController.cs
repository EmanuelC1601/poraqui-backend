using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Poraqui.Application.Commerce;
using Poraqui.Application.Security;
using Poraqui.Infrastructure.Identity;

namespace Poraqui.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class CatalogController(CatalogOrderService catalog, UserManager<ApplicationUser> users) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("businesses/{businessId:guid}/opening-hours")]
    public async Task<IActionResult> GetOpeningHours(Guid businessId, CancellationToken cancellationToken)
    {
        var result = await catalog.GetOpeningHoursAsync(businessId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [Authorize(Policy = ApplicationPolicies.ManageBusiness)]
    [HttpPut("businesses/{businessId:guid}/opening-hours")]
    public async Task<IActionResult> ConfigureOpeningHours(Guid businessId, ConfigureOpeningHoursRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var isAdmin)) return Unauthorized();
        if (request.Days is null) return BadRequest(new { error = "A weekly schedule is required." });
        try
        {
            var result = await catalog.ConfigureOpeningHoursAsync(businessId, actorId, isAdmin,
                request.TimeZoneId, request.Days, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (BusinessAuthorizationException) { return Forbid(); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [AllowAnonymous]
    [HttpGet("businesses/{businessId:guid}/products")]
    public async Task<IActionResult> GetProducts(Guid businessId, CancellationToken cancellationToken) =>
        Ok(await catalog.GetProductsAsync(businessId, cancellationToken));

    [Authorize(Policy = ApplicationPolicies.ManageBusiness)]
    [HttpPost("businesses/{businessId:guid}/products")]
    public async Task<IActionResult> AddProduct(Guid businessId, ProductRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var isAdmin)) return Unauthorized();
        try
        {
            var result = await catalog.AddProductAsync(businessId, actorId, request.Name, request.Price, cancellationToken, isAdmin);
            return Created($"/api/businesses/{businessId}/products/{result!.Id}", result);
        }
        catch (BusinessAuthorizationException) when (!isAdmin) { return Forbid(); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [Authorize(Policy = ApplicationPolicies.ManageBusiness)]
    [HttpPut("products/{productId:guid}")]
    public async Task<IActionResult> UpdateProduct(Guid productId, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var isAdmin)) return Unauthorized();
        try
        {
            var result = await catalog.UpdateProductAsync(productId, actorId, request.Name, request.Price, request.IsAvailable, cancellationToken, isAdmin);
            return result is null ? NotFound() : Ok(result);
        }
        catch (BusinessAuthorizationException) { return Forbid(); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [Authorize(Policy = ApplicationPolicies.ManageBusiness)]
    [HttpPost("products/{productId:guid}/option-groups")]
    public async Task<IActionResult> AddOptionGroup(Guid productId, OptionGroupRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var isAdmin)) return Unauthorized();
        try
        {
            var group = await catalog.AddOptionGroupAsync(productId, actorId, isAdmin, request, cancellationToken);
            return group is null ? NotFound() : Created($"/api/option-groups/{group.Id}", group);
        }
        catch (BusinessAuthorizationException) { return Forbid(); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [Authorize(Policy = ApplicationPolicies.ManageBusiness)]
    [HttpPut("option-groups/{optionGroupId:guid}/availability")]
    public async Task<IActionResult> SetOptionGroupAvailability(Guid optionGroupId, AvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var isAdmin)) return Unauthorized();
        try
        {
            var group = await catalog.SetOptionGroupAvailabilityAsync(optionGroupId, actorId, isAdmin,
                request.IsAvailable, cancellationToken);
            return group is null ? NotFound() : Ok(group);
        }
        catch (BusinessAuthorizationException) { return Forbid(); }
    }

    [Authorize(Policy = ApplicationPolicies.ManageBusiness)]
    [HttpPut("product-options/{optionId:guid}/availability")]
    public async Task<IActionResult> SetOptionAvailability(Guid optionId, AvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var isAdmin)) return Unauthorized();
        try
        {
            var option = await catalog.SetOptionAvailabilityAsync(optionId, actorId, isAdmin,
                request.IsAvailable, cancellationToken);
            return option is null ? NotFound() : Ok(option);
        }
        catch (BusinessAuthorizationException) { return Forbid(); }
    }

    [AllowAnonymous]
    [HttpGet("delivery-zones/active")]
    public async Task<IActionResult> GetZones(CancellationToken cancellationToken) => Ok(await catalog.GetZonesAsync(cancellationToken));

    [Authorize(Policy = ApplicationPolicies.PlatformAdministration)]
    [HttpPost("delivery-zones")]
    public async Task<IActionResult> AddZone(ZoneRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await catalog.AddZoneAsync(request.Name, cancellationToken)); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [Authorize(Policy = ApplicationPolicies.PlatformAdministration)]
    [HttpPut("delivery-zones/{zoneId:guid}")]
    public async Task<IActionResult> UpdateZone(Guid zoneId, UpdateZoneRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await catalog.UpdateZoneAsync(zoneId, request.Name, request.IsActive, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [Authorize(Policy = ApplicationPolicies.ManageBusiness)]
    [HttpGet("businesses/{businessId:guid}/delivery-rates")]
    public async Task<IActionResult> GetRates(Guid businessId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var isAdmin)) return Unauthorized();
        try
        {
            return Ok(isAdmin
                ? await catalog.GetRatesForAdminAsync(businessId, cancellationToken)
                : await catalog.GetRatesAsync(businessId, actorId, cancellationToken));
        }
        catch (BusinessAuthorizationException) { return Forbid(); }
    }

    [Authorize(Policy = ApplicationPolicies.ManageBusiness)]
    [HttpPost("businesses/{businessId:guid}/delivery-rates")]
    public async Task<IActionResult> AddRate(Guid businessId, RateRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var isAdmin)) return Unauthorized();
        try
        {
            var result = await catalog.AddRateAsync(businessId, actorId, request.ZoneId, request.Fee, cancellationToken, isAdmin);
            return Ok(result);
        }
        catch (BusinessAuthorizationException) { return Forbid(); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [Authorize(Policy = ApplicationPolicies.ManageBusiness)]
    [HttpPut("businesses/{businessId:guid}/delivery-rates/{zoneId:guid}")]
    public async Task<IActionResult> UpdateRate(Guid businessId, Guid zoneId, UpdateRateRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var isAdmin)) return Unauthorized();
        try
        {
            var result = await catalog.UpdateRateAsync(businessId, actorId, zoneId, request.Fee, request.IsActive, cancellationToken, isAdmin);
            return result is null ? NotFound() : Ok(result);
        }
        catch (BusinessAuthorizationException) { return Forbid(); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [Authorize]
    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(users.GetUserId(User), out var customerId)) return Unauthorized();
        try
        {
            var order = await catalog.CreateOrderAsync(customerId, request.BusinessId, request.ZoneId,
                request.DeliveryAddress, request.Items, cancellationToken);
            return Created($"/api/orders/{order.Id}", order);
        }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
    }

    private bool TryGetActor(out Guid actorId, out bool isAdmin)
    {
        isAdmin = User.IsInRole(ApplicationRoles.PlatformAdmin);
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? users.GetUserId(User), out actorId);
    }
}

public sealed record ProductRequest(string Name, decimal Price);
public sealed record UpdateProductRequest(string Name, decimal Price, bool IsAvailable);
public sealed record ZoneRequest(string Name);
public sealed record UpdateZoneRequest(string Name, bool IsActive);
public sealed record RateRequest(Guid ZoneId, decimal Fee);
public sealed record UpdateRateRequest(decimal Fee, bool IsActive);
public sealed record ConfigureOpeningHoursRequest(string TimeZoneId, IReadOnlyCollection<OpeningHoursRequest> Days);
public sealed record CreateOrderRequest(Guid BusinessId, Guid ZoneId, string DeliveryAddress, IReadOnlyCollection<OrderItemRequest> Items);
