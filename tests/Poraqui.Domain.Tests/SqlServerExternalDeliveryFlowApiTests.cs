using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poraqui.Application.Security;
using Poraqui.Domain.Commerce;
using Poraqui.Domain.Orders;
using Poraqui.Infrastructure.Identity;
using Poraqui.Infrastructure.Persistence;

namespace Poraqui.Domain.Tests;

public sealed class SqlServerExternalDeliveryFlowApiTests
{
    private const string TestPassword = "Test-Password-1!";

    [SqlServerIntegrationFact]
    public async Task Sql_server_api_flow_dispatches_accepts_and_delivers_one_external_order()
    {
        await using var factory = new SqlServerApiTestHost();
        using var client = factory.CreateIsolatedClient();
        await factory.InitializeDatabaseAsync();

        var suffix = Guid.NewGuid().ToString("N");
        var adminEmail = $"sql-ext-admin-{suffix}@example.test";
        var merchantEmail = $"sql-ext-merchant-{suffix}@example.test";
        var customerEmail = $"sql-ext-customer-{suffix}@example.test";
        var courierEmail = $"sql-ext-courier-{suffix}@example.test";
        Guid? adminId = null;
        Guid? merchantId = null;
        Guid? customerId = null;
        Guid? courierId = null;
        Guid? businessId = null;
        Guid? zoneId = null;
        Guid? productId = null;
        Guid? orderId = null;
        string? deliveryCode = null;

        try
        {
            adminId = await CreateAdminAsync(factory.Services, adminEmail);
            var adminToken = await LoginAsync(client, adminEmail);
            await RegisterAsync(client, merchantEmail);
            var merchantToken = await LoginAsync(client, merchantEmail);
            merchantId = await GetCurrentUserIdAsync(factory.Services, merchantEmail);
            businessId = await RegisterBusinessAsync(client, merchantToken, "SQL external business " + suffix);
            merchantToken = await LoginAsync(client, merchantEmail);
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Put,
                $"api/businesses/{businessId}/transfer-details", merchantToken,
                JsonContent.Create(new { recipientName = "SQL Test Recipient", institution = "Test Institution",
                    accountReference = "SQLTEST-EXT-ACCOUNT", instructions = "External courier test transfer." }))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Put,
                $"api/businesses/{businessId}/active", adminToken, JsonContent.Create(new { active = true }))).StatusCode);

            using (var zoneResponse = await SendAsync(client, HttpMethod.Post, "api/delivery-zones", adminToken,
                       JsonContent.Create(new { name = "SQL external zone " + suffix })))
            {
                Assert.Equal(HttpStatusCode.OK, zoneResponse.StatusCode);
                using var body = JsonDocument.Parse(await zoneResponse.Content.ReadAsStringAsync());
                zoneId = body.RootElement.GetProperty("id").GetGuid();
            }
            using (var productResponse = await SendAsync(client, HttpMethod.Post,
                       $"api/businesses/{businessId}/products", merchantToken,
                       JsonContent.Create(new { name = "SQL external meal", price = 84.25m })))
            {
                Assert.Equal(HttpStatusCode.Created, productResponse.StatusCode);
                using var body = JsonDocument.Parse(await productResponse.Content.ReadAsStringAsync());
                productId = body.RootElement.GetProperty("id").GetGuid();
            }
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Post,
                $"api/businesses/{businessId}/delivery-rates", merchantToken,
                JsonContent.Create(new { zoneId, fee = 15m }))).StatusCode);

            await RegisterAsync(client, customerEmail);
            var customerToken = await LoginAsync(client, customerEmail);
            customerId = await GetCurrentUserIdAsync(factory.Services, customerEmail);
            var phoneNumber = $"+5255{Random.Shared.Next(10_000_000, 99_999_999)}";
            Assert.Equal(HttpStatusCode.Accepted, (await SendAsync(client, HttpMethod.Post,
                "api/account/phone/verification", customerToken, JsonContent.Create(new { phoneNumber }))).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post,
                "api/account/phone/verification/confirm", customerToken,
                JsonContent.Create(new { phoneNumber, code = "123456" }))).StatusCode);

            using (var create = await SendAsync(client, HttpMethod.Post, "api/orders", customerToken,
                       JsonContent.Create(new { commandId = Guid.NewGuid(), businessId, zoneId,
                           deliveryAddress = "SQL external destination " + suffix,
                           items = new[] { new { productId, quantity = 1, unitPrice = 0.01m } } })))
            {
                Assert.Equal(HttpStatusCode.Created, create.StatusCode);
                using var body = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
                orderId = body.RootElement.GetProperty("id").GetGuid();
                deliveryCode = body.RootElement.GetProperty("customerDeliveryCode").GetString();
                Assert.Equal(84.25m, body.RootElement.GetProperty("subtotal").GetDecimal());
                Assert.Equal(15m, body.RootElement.GetProperty("deliveryFee").GetDecimal());
                Assert.Equal(99.25m, body.RootElement.GetProperty("total").GetDecimal());
            }

            using (var evidence = new MultipartFormDataContent())
            {
                var pdf = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.7 isolated SQL external test"));
                pdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                evidence.Add(new StringContent(Guid.NewGuid().ToString()), "commandId");
                evidence.Add(pdf, "file", "sql-external-receipt.pdf");
                using var request = new HttpRequestMessage(HttpMethod.Post, $"api/orders/{orderId}/transfer-evidence") { Content = evidence };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);
                using var upload = await client.SendAsync(request);
                Assert.Equal(HttpStatusCode.Accepted, upload.StatusCode);
            }
            Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post,
                $"api/businesses/{businessId}/orders/{orderId}/payment-review", merchantToken,
                JsonContent.Create(new { approved = true, note = "External flow transfer reviewed." }))).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post,
                $"api/businesses/{businessId}/orders/{orderId}/decision", merchantToken,
                JsonContent.Create(new { accept = true, reason = (string?)null }))).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post,
                $"api/businesses/{businessId}/orders/{orderId}/preparation", merchantToken)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post,
                $"api/businesses/{businessId}/orders/{orderId}/ready", merchantToken)).StatusCode);

            await RegisterAsync(client, courierEmail);
            courierId = await GetCurrentUserIdAsync(factory.Services, courierEmail);
            Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Put,
                $"api/admin/users/{courierId}/external-courier", adminToken,
                JsonContent.Create(new { isExternalCourier = true }))).StatusCode);
            var courierToken = await LoginAsync(client, courierEmail);
            Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Put,
                "api/courier/external/availability", courierToken,
                JsonContent.Create(new { isAvailable = true, zoneId }))).StatusCode);

            using (var dispatch = await SendAsync(client, HttpMethod.Post,
                       $"api/businesses/{businessId}/orders/{orderId}/external-offers", merchantToken,
                       JsonContent.Create(new { remuneration = 32.75m, conditions = "Negocio paga en efectivo al recoger", expiresInMinutes = 15 })))
            {
                Assert.Equal(HttpStatusCode.Accepted, dispatch.StatusCode);
                using var body = JsonDocument.Parse(await dispatch.Content.ReadAsStringAsync());
                Assert.Equal(1, body.RootElement.GetProperty("offerCount").GetInt32());
                Assert.Equal("OffersCreated", body.RootElement.GetProperty("outcome").GetString());
            }

            Guid offerId;
            using (var offersResponse = await SendAsync(client, HttpMethod.Get, "api/courier/external/offers", courierToken))
            using (var offersBody = JsonDocument.Parse(await offersResponse.Content.ReadAsStringAsync()))
            {
                Assert.Equal(HttpStatusCode.OK, offersResponse.StatusCode);
                var offer = Assert.Single(offersBody.RootElement.EnumerateArray());
                offerId = offer.GetProperty("id").GetGuid();
                Assert.Equal("SQL integration pickup address", offer.GetProperty("pickupAddress").GetString());
                Assert.Equal("SQL external destination " + suffix, offer.GetProperty("destinationAddress").GetString());
                Assert.Equal(32.75m, offer.GetProperty("remuneration").GetDecimal());
                Assert.Equal("Negocio paga en efectivo al recoger", offer.GetProperty("conditions").GetString());
            }

            using var accept = await SendAsync(client, HttpMethod.Post,
                $"api/courier/external/offers/{offerId}/accept", courierToken);
            Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
            using var acceptBody = JsonDocument.Parse(await accept.Content.ReadAsStringAsync());
            var pickupCode = acceptBody.RootElement.GetProperty("pickupCode").GetString()!;
            Assert.Equal(6, pickupCode.Length);
            using (var replay = await SendAsync(client, HttpMethod.Post,
                       $"api/courier/external/offers/{offerId}/accept", courierToken))
            using (var replayBody = JsonDocument.Parse(await replay.Content.ReadAsStringAsync()))
            {
                Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
                Assert.Equal(pickupCode, replayBody.RootElement.GetProperty("pickupCode").GetString());
            }

            Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post,
                $"api/orders/{orderId}/pickup", courierToken, JsonContent.Create(new { code = pickupCode }))).StatusCode);
            using (var businessOrder = await SendAsync(client, HttpMethod.Get,
                       $"api/businesses/{businessId}/orders/{orderId}", merchantToken))
            using (var body = JsonDocument.Parse(await businessOrder.Content.ReadAsStringAsync()))
            {
                Assert.Equal(HttpStatusCode.OK, businessOrder.StatusCode);
                Assert.True(body.RootElement.GetProperty("courierPaymentReceived").GetBoolean());
                Assert.Equal(32.75m, body.RootElement.GetProperty("courierPaymentAmount").GetDecimal());
            }
            using (var start = await SendAsync(client, HttpMethod.Post,
                       $"api/orders/{orderId}/delivery/start", courierToken, JsonContent.Create(new { contactChannel = "call" })))
            using (var body = JsonDocument.Parse(await start.Content.ReadAsStringAsync()))
            {
                Assert.Equal(HttpStatusCode.OK, start.StatusCode);
                Assert.Equal(phoneNumber, body.RootElement.GetProperty("phoneNumber").GetString());
                Assert.Equal("call", body.RootElement.GetProperty("channel").GetString());
            }
            using (var orderForCode = await SendAsync(client, HttpMethod.Get, $"api/orders/{orderId}", customerToken))
            using (var body = JsonDocument.Parse(await orderForCode.Content.ReadAsStringAsync()))
            {
                Assert.Equal(HttpStatusCode.OK, orderForCode.StatusCode);
                Assert.Equal("InTransit", body.RootElement.GetProperty("status").GetString());
                Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(client, HttpMethod.Post,
                    $"api/orders/{orderId}/delivery/confirm", courierToken,
                    JsonContent.Create(new { code = "111111" }))).StatusCode);
                using var confirm = await SendAsync(client, HttpMethod.Post,
                    $"api/orders/{orderId}/delivery/confirm", courierToken,
                    JsonContent.Create(new { code = deliveryCode ?? throw new InvalidOperationException("Test delivery code was not returned.") }));
                Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);
            }
            using (var finalOrder = await SendAsync(client, HttpMethod.Get, $"api/orders/{orderId}", customerToken))
            using (var body = JsonDocument.Parse(await finalOrder.Content.ReadAsStringAsync()))
            {
                Assert.Equal(HttpStatusCode.OK, finalOrder.StatusCode);
                Assert.Equal("Delivered", body.RootElement.GetProperty("status").GetString());
                Assert.True(body.RootElement.GetProperty("history").GetArrayLength() >= 8);
            }
        }
        finally
        {
            await CleanupAsync(factory.Services, adminId, merchantId, customerId, courierId, businessId, zoneId, productId, orderId);
        }
    }

    private static async Task RegisterAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync("api/auth/register", new { email, password = TestPassword });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync("api/auth/login?useCookies=false", new { email, password = TestPassword });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static async Task<Guid> RegisterBusinessAsync(HttpClient client, string token, string name)
    {
        using var response = await SendAsync(client, HttpMethod.Post, "api/businesses", token,
            JsonContent.Create(new { name, address = "SQL integration pickup address" }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path,
        string token, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private static async Task<Guid> GetCurrentUserIdAsync(IServiceProvider services, string email)
    {
        using var scope = services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync(email) ?? throw new InvalidOperationException("Test user was not created.");
        return user.Id;
    }

    private static async Task<Guid> CreateAdminAsync(IServiceProvider services, string email)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var roles = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roles.RoleExistsAsync(ApplicationRoles.PlatformAdmin))
        {
            var roleResult = await roles.CreateAsync(new IdentityRole<Guid>(ApplicationRoles.PlatformAdmin));
            Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(error => error.Description)));
        }
        var users = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = "SQL External Integration Admin" };
        var createResult = await users.CreateAsync(admin, TestPassword);
        Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(error => error.Description)));
        var addRoleResult = await users.AddToRoleAsync(admin, ApplicationRoles.PlatformAdmin);
        Assert.True(addRoleResult.Succeeded, string.Join("; ", addRoleResult.Errors.Select(error => error.Description)));
        return admin.Id;
    }

    private static async Task CleanupAsync(IServiceProvider services, Guid? adminId, Guid? merchantId, Guid? customerId,
        Guid? courierId, Guid? businessId, Guid? zoneId, Guid? productId, Guid? orderId)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ChangeTracker.Clear();
        if (orderId is { } orderKey)
        {
            db.Set<CourierAssignmentCommand>().RemoveRange(await db.Set<CourierAssignmentCommand>()
                .Where(command => command.OrderId == orderKey).ToArrayAsync());
            db.Set<OrderCreationCommand>().RemoveRange(await db.Set<OrderCreationCommand>()
                .Where(command => command.OrderId == orderKey).ToArrayAsync());
            var order = await db.Orders.SingleOrDefaultAsync(candidate => candidate.Id == orderKey);
            if (order is not null) db.Orders.Remove(order);
            await db.SaveChangesAsync();
        }
        if (productId is { } productKey)
        {
            var product = await db.CatalogProducts.SingleOrDefaultAsync(candidate => candidate.Id == productKey);
            if (product is not null) db.CatalogProducts.Remove(product);
        }
        if (businessId is { } businessKey)
        {
            db.DeliveryRates.RemoveRange(await db.DeliveryRates.Where(rate => rate.BusinessId == businessKey).ToArrayAsync());
            await db.SaveChangesAsync();
            var business = await db.Businesses.SingleOrDefaultAsync(candidate => candidate.Id == businessKey);
            if (business is not null) db.Businesses.Remove(business);
        }
        if (zoneId is { } zoneKey)
        {
            var zone = await db.DeliveryZones.SingleOrDefaultAsync(candidate => candidate.Id == zoneKey);
            if (zone is not null) db.DeliveryZones.Remove(zone);
        }
        await db.SaveChangesAsync();
        var userIds = new[] { adminId, merchantId, customerId, courierId }.OfType<Guid>().ToArray();
        db.UserRoles.RemoveRange(await db.UserRoles.Where(role => userIds.Contains(role.UserId)).ToArrayAsync());
        db.Users.RemoveRange(await db.Users.Where(user => userIds.Contains(user.Id)).ToArrayAsync());
        await db.SaveChangesAsync();
    }
}
