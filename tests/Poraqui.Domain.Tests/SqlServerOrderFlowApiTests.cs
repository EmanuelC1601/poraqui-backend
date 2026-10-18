using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

[Collection("SQL Server API integration")]
public sealed class SqlServerOrderFlowApiTests
{
    private const string TestPassword = "Test-Password-1!";

    [SqlServerIntegrationFact]
    public async Task Sql_server_api_flow_creates_reviews_prepares_assigns_and_delivers_one_order()
    {
        await using var factory = new SqlServerApiTestHost();
        using var client = factory.CreateIsolatedClient();
        await factory.InitializeDatabaseAsync();

        var suffix = Guid.NewGuid().ToString("N");
        var adminEmail = $"sql-admin-{suffix}@example.test";
        var merchantEmail = $"sql-merchant-{suffix}@example.test";
        var customerEmail = $"sql-customer-{suffix}@example.test";
        var courierEmail = $"sql-courier-{suffix}@example.test";
        Guid? adminId = null;
        Guid? merchantId = null;
        Guid? customerId = null;
        Guid? courierId = null;
        Guid? businessId = null;
        Guid? zoneId = null;
        Guid? productId = null;
        Guid? orderId = null;

        try
        {
            adminId = await CreateAdminAsync(factory.Services, adminEmail);
            var adminToken = await LoginAsync(client, adminEmail);

            await RegisterAsync(client, merchantEmail);
            var merchantToken = await LoginAsync(client, merchantEmail);
            merchantId = await GetCurrentUserIdAsync(factory.Services, merchantEmail);
            businessId = await RegisterBusinessAsync(client, merchantToken, "SQL integration business " + suffix);
            merchantToken = await LoginAsync(client, merchantEmail);
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Put,
                $"api/businesses/{businessId}/transfer-details", merchantToken,
                JsonContent.Create(new { recipientName = "SQL Test Recipient", institution = "Test Institution",
                    accountReference = "SQLTEST-ACCOUNT", instructions = "Transfer test only." }))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Put,
                $"api/businesses/{businessId}/active", adminToken, JsonContent.Create(new { active = true }))).StatusCode);

            using (var zoneResponse = await SendAsync(client, HttpMethod.Post, "api/delivery-zones", adminToken,
                       JsonContent.Create(new { name = "SQL integration zone " + suffix })))
            {
                Assert.Equal(HttpStatusCode.OK, zoneResponse.StatusCode);
                using var zoneBody = JsonDocument.Parse(await zoneResponse.Content.ReadAsStringAsync());
                zoneId = zoneBody.RootElement.GetProperty("id").GetGuid();
            }
            using (var productResponse = await SendAsync(client, HttpMethod.Post,
                       $"api/businesses/{businessId}/products", merchantToken,
                       JsonContent.Create(new { name = "SQL integration meal", price = 50m })))
            {
                Assert.Equal(HttpStatusCode.Created, productResponse.StatusCode);
                using var productBody = JsonDocument.Parse(await productResponse.Content.ReadAsStringAsync());
                productId = productBody.RootElement.GetProperty("id").GetGuid();
            }
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Post,
                $"api/businesses/{businessId}/delivery-rates", merchantToken,
                JsonContent.Create(new { zoneId, fee = 12.50m }))).StatusCode);

            await RegisterAsync(client, customerEmail);
            var customerToken = await LoginAsync(client, customerEmail);
            customerId = await GetCurrentUserIdAsync(factory.Services, customerEmail);
            var phoneNumber = $"+5255{Random.Shared.Next(10_000_000, 99_999_999)}";
            using (var started = await SendAsync(client, HttpMethod.Post, "api/account/phone/verification", customerToken,
                       JsonContent.Create(new { phoneNumber })))
                Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);
            using (var confirmed = await SendAsync(client, HttpMethod.Post, "api/account/phone/verification/confirm", customerToken,
                       JsonContent.Create(new { phoneNumber, code = "123456" })))
                Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);

            var commandId = Guid.NewGuid();
            using var createOrder = await SendAsync(client, HttpMethod.Post, "api/orders", customerToken,
                JsonContent.Create(new { commandId, businessId, zoneId, deliveryAddress = "SQL test delivery address",
                    items = new[] { new { productId, quantity = 2, unitPrice = 0.01m } } }));
            Assert.Equal(HttpStatusCode.Created, createOrder.StatusCode);
            using var orderBody = JsonDocument.Parse(await createOrder.Content.ReadAsStringAsync());
            orderId = orderBody.RootElement.GetProperty("id").GetGuid();
            var deliveryCode = orderBody.RootElement.GetProperty("customerDeliveryCode").GetString()!;
            Assert.Equal(100m, orderBody.RootElement.GetProperty("subtotal").GetDecimal());
            Assert.Equal(12.50m, orderBody.RootElement.GetProperty("deliveryFee").GetDecimal());
            Assert.Equal(112.50m, orderBody.RootElement.GetProperty("total").GetDecimal());

            using (var evidence = new MultipartFormDataContent())
            {
                var pdf = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("%PDF-1.7 isolated SQL test"));
                pdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                evidence.Add(new StringContent(Guid.NewGuid().ToString()), "commandId");
                evidence.Add(pdf, "file", "sql-test-receipt.pdf");
                using var request = new HttpRequestMessage(HttpMethod.Post, $"api/orders/{orderId}/transfer-evidence") { Content = evidence };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);
                using var uploaded = await client.SendAsync(request);
                Assert.Equal(HttpStatusCode.Accepted, uploaded.StatusCode);
            }
            Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post,
                $"api/businesses/{businessId}/orders/{orderId}/payment-review", merchantToken,
                JsonContent.Create(new { approved = true, note = "Received in test bank review." }))).StatusCode);
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
                $"api/admin/users/{courierId}/delivery-person", adminToken,
                JsonContent.Create(new { isDeliveryPerson = true }))).StatusCode);
            var courierToken = await LoginAsync(client, courierEmail);
            using var assignment = await SendAsync(client, HttpMethod.Post,
                $"api/businesses/{businessId}/orders/{orderId}/assign-courier", merchantToken,
                JsonContent.Create(new { courierUserId = courierId, courierPaymentAmount = 30m, commandId = Guid.NewGuid() }));
            Assert.Equal(HttpStatusCode.OK, assignment.StatusCode);
            using var assignmentBody = JsonDocument.Parse(await assignment.Content.ReadAsStringAsync());
            var pickupCode = assignmentBody.RootElement.GetProperty("pickupCode").GetString()!;
            Assert.Equal(30m, assignmentBody.RootElement.GetProperty("paymentAmount").GetDecimal());

            Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post,
                $"api/orders/{orderId}/pickup", courierToken, JsonContent.Create(new { code = pickupCode }))).StatusCode);
            using (var businessOrder = await SendAsync(client, HttpMethod.Get,
                       $"api/businesses/{businessId}/orders/{orderId}", merchantToken))
            using (var businessOrderBody = JsonDocument.Parse(await businessOrder.Content.ReadAsStringAsync()))
            {
                Assert.Equal(HttpStatusCode.OK, businessOrder.StatusCode);
                Assert.True(businessOrderBody.RootElement.GetProperty("courierPaymentReceived").GetBoolean());
                Assert.Equal(30m, businessOrderBody.RootElement.GetProperty("courierPaymentAmount").GetDecimal());
            }
            using (var startedDelivery = await SendAsync(client, HttpMethod.Post,
                       $"api/orders/{orderId}/delivery/start", courierToken, JsonContent.Create(new { contactChannel = "whatsapp" })))
            using (var contact = JsonDocument.Parse(await startedDelivery.Content.ReadAsStringAsync()))
            {
                Assert.Equal(HttpStatusCode.OK, startedDelivery.StatusCode);
                Assert.Equal(phoneNumber, contact.RootElement.GetProperty("phoneNumber").GetString());
                Assert.Equal("whatsapp", contact.RootElement.GetProperty("channel").GetString());
            }
            Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post,
                $"api/orders/{orderId}/delivery/confirm", courierToken, JsonContent.Create(new { code = deliveryCode }))).StatusCode);
            using (var customerOrder = await SendAsync(client, HttpMethod.Get, $"api/orders/{orderId}", customerToken))
            using (var customerOrderBody = JsonDocument.Parse(await customerOrder.Content.ReadAsStringAsync()))
            {
                Assert.Equal(HttpStatusCode.OK, customerOrder.StatusCode);
                Assert.Equal("Delivered", customerOrderBody.RootElement.GetProperty("status").GetString());
                Assert.True(customerOrderBody.RootElement.GetProperty("history").GetArrayLength() >= 8);
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
        var admin = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = "SQL Integration Admin" };
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
        if (businessId is { } businessIdKey)
        {
            db.DeliveryRates.RemoveRange(await db.DeliveryRates.Where(rate => rate.BusinessId == businessIdKey).ToArrayAsync());
            await db.SaveChangesAsync();
            var business = await db.Businesses.SingleOrDefaultAsync(candidate => candidate.Id == businessIdKey);
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
