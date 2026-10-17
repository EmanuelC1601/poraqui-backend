using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Poraqui.Application.Security;
using Poraqui.Application.Orders;
using Poraqui.Infrastructure.Identity;

namespace Poraqui.Domain.Tests;

public sealed class BusinessApiTests
{
    [Fact]
    public async Task Authenticated_business_owners_can_manage_only_their_own_business()
    {
        await using var factory = new ApiTestHost();
        using var client = factory.CreateClient();
        await factory.InitializeDatabaseAsync();
        var routeNames = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .Select(endpoint => endpoint.DisplayName ?? string.Empty).ToArray();
        Assert.Contains(routeNames, route => route.Contains("POST /api/auth/register", StringComparison.Ordinal));

        await RegisterAsync(client, "first-owner@example.test");
        var firstToken = await LoginAsync(client, "first-owner@example.test");
        var firstBusinessId = await RegisterBusinessAsync(client, firstToken, "Taquería Uno");
        firstToken = await LoginAsync(client, "first-owner@example.test");
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, $"api/businesses/{firstBusinessId}", firstToken)).StatusCode);

        await RegisterAsync(client, "second-owner@example.test");
        var secondToken = await LoginAsync(client, "second-owner@example.test");

        var weeklyHours = new
        {
            timeZoneId = "UTC",
            days = Enum.GetValues<DayOfWeek>().Select(day => new { day = (int)day, isClosed = false,
                opensAt = "00:00:00", closesAt = "23:59:59" }).ToArray()
        };
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Put,
            $"api/businesses/{firstBusinessId}/opening-hours", secondToken, JsonContent.Create(weeklyHours))).StatusCode);
        var secondBusinessId = await RegisterBusinessAsync(client, secondToken, "Taquería Dos");
        secondToken = await LoginAsync(client, "second-owner@example.test");

        using var productResponse = await SendAsync(client, HttpMethod.Post, $"api/businesses/{firstBusinessId}/products", firstToken,
            JsonContent.Create(new { name = "Producto ajeno", price = 10m }));
        Assert.Equal(HttpStatusCode.Created, productResponse.StatusCode);
        using var productJson = JsonDocument.Parse(await productResponse.Content.ReadAsStringAsync());
        var productId = productJson.RootElement.GetProperty("id").GetGuid();

        Assert.NotEqual(firstBusinessId, secondBusinessId);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendAsync(client, HttpMethod.Get, $"api/businesses/{firstBusinessId}", secondToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendAsync(client, HttpMethod.Put, $"api/businesses/{firstBusinessId}", secondToken,
                JsonContent.Create(new { name = "Cambio ajeno", address = "Centro" }))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendAsync(client, HttpMethod.Put, $"api/businesses/{firstBusinessId}/transfer-details", secondToken,
                JsonContent.Create(new { recipientName = "Otro", institution = "Otro banco", accountReference = "123456", instructions = "" }))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendAsync(client, HttpMethod.Get, $"api/businesses/{firstBusinessId}/transfer-details", secondToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendAsync(client, HttpMethod.Put, $"api/products/{productId}", secondToken,
                JsonContent.Create(new { name = "Cambio ajeno", price = 1m, isAvailable = false }))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("api/businesses/active")).StatusCode);
    }

    [Fact]
    public async Task Manual_payment_and_internal_delivery_flow_preserves_server_pricing_and_history()
    {
        await using var factory = new ApiTestHost();
        using var client = factory.CreateClient();
        await factory.InitializeDatabaseAsync();

        var bootstrap = await client.PostAsJsonAsync("api/auth/bootstrap-admin", new
        {
            email = "admin@example.test", displayName = "Test Admin", password = "Test-Password-1!",
            bootstrapKey = "integration-test-bootstrap-key-with-sufficient-length"
        });
        Assert.Equal(HttpStatusCode.Created, bootstrap.StatusCode);
        var adminToken = await LoginAsync(client, "admin@example.test");

        await RegisterAsync(client, "merchant@example.test");
        var merchantToken = await LoginAsync(client, "merchant@example.test");
        var businessId = await RegisterBusinessAsync(client, merchantToken, "Mercado de prueba");
        merchantToken = await LoginAsync(client, "merchant@example.test");
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Put,
            $"api/businesses/{businessId}/active", adminToken, JsonContent.Create(new { active = true }))).StatusCode);
        using var zoneResponse = await SendAsync(client, HttpMethod.Post, "api/delivery-zones", adminToken,
            JsonContent.Create(new { name = "Centro de prueba" }));
        Assert.Equal(HttpStatusCode.OK, zoneResponse.StatusCode);
        using var zoneJson = JsonDocument.Parse(await zoneResponse.Content.ReadAsStringAsync());
        var zoneId = zoneJson.RootElement.GetProperty("id").GetGuid();

        using var productResponse = await SendAsync(client, HttpMethod.Post, $"api/businesses/{businessId}/products", merchantToken,
            JsonContent.Create(new { name = "Producto de prueba", price = 25.50m }));
        Assert.Equal(HttpStatusCode.Created, productResponse.StatusCode);
        using var productJson = JsonDocument.Parse(await productResponse.Content.ReadAsStringAsync());
        var productId = productJson.RootElement.GetProperty("id").GetGuid();

        using var optionGroupResponse = await SendAsync(client, HttpMethod.Post,
            $"api/products/{productId}/option-groups", merchantToken,
            JsonContent.Create(new
            {
                name = "Tamaño", minSelections = 1, maxSelections = 1,
                options = new[] { new { name = "Regular", priceDelta = 0m }, new { name = "Grande", priceDelta = 5.50m } }
            }));
        Assert.Equal(HttpStatusCode.Created, optionGroupResponse.StatusCode);
        using var optionGroupJson = JsonDocument.Parse(await optionGroupResponse.Content.ReadAsStringAsync());
        var optionGroupId = optionGroupJson.RootElement.GetProperty("id").GetGuid();
        var largeOptionId = optionGroupJson.RootElement.GetProperty("options").EnumerateArray()
            .Single(option => option.GetProperty("name").GetString() == "Grande").GetProperty("id").GetGuid();
        var regularOptionId = optionGroupJson.RootElement.GetProperty("options").EnumerateArray()
            .Single(option => option.GetProperty("name").GetString() == "Regular").GetProperty("id").GetGuid();
        using var extrasGroupResponse = await SendAsync(client, HttpMethod.Post,
            $"api/products/{productId}/option-groups", merchantToken,
            JsonContent.Create(new
            {
                name = "Extras", minSelections = 0, maxSelections = 2,
                options = new[] { new { name = "Queso", priceDelta = 7.25m }, new { name = "Tocino", priceDelta = 10m } }
            }));
        Assert.Equal(HttpStatusCode.Created, extrasGroupResponse.StatusCode);
        using var extrasGroupJson = JsonDocument.Parse(await extrasGroupResponse.Content.ReadAsStringAsync());
        var cheeseOptionId = extrasGroupJson.RootElement.GetProperty("options").EnumerateArray()
            .Single(option => option.GetProperty("name").GetString() == "Queso").GetProperty("id").GetGuid();
        var baconOptionId = extrasGroupJson.RootElement.GetProperty("options").EnumerateArray()
            .Single(option => option.GetProperty("name").GetString() == "Tocino").GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Put,
            $"api/product-options/{baconOptionId}/availability", merchantToken,
            JsonContent.Create(new { isAvailable = false }))).StatusCode);
        using (var catalogResponse = await client.GetAsync($"api/businesses/{businessId}/products"))
        {
            using var catalogJson = JsonDocument.Parse(await catalogResponse.Content.ReadAsStringAsync());
            Assert.Equal(2, catalogJson.RootElement[0].GetProperty("optionGroups").GetArrayLength());
            Assert.Single(catalogJson.RootElement[0].GetProperty("optionGroups").EnumerateArray()
                .Single(group => group.GetProperty("name").GetString() == "Extras").GetProperty("options").EnumerateArray());
        }

        using var rateResponse = await SendAsync(client, HttpMethod.Post, $"api/businesses/{businessId}/delivery-rates", merchantToken,
            JsonContent.Create(new { zoneId, fee = 18.25m }));
        Assert.Equal(HttpStatusCode.OK, rateResponse.StatusCode);
        var alwaysOpenHours = new
        {
            timeZoneId = "UTC",
            days = Enum.GetValues<DayOfWeek>().Select(day => new { day = (int)day, isClosed = false,
                opensAt = "00:00:00", closesAt = "23:59:59" }).ToArray()
        };
        using (var hoursResponse = await SendAsync(client, HttpMethod.Put,
                   $"api/businesses/{businessId}/opening-hours", merchantToken, JsonContent.Create(alwaysOpenHours)))
            Assert.Equal(HttpStatusCode.OK, hoursResponse.StatusCode);
        using (var publicHours = await client.GetAsync($"api/businesses/{businessId}/opening-hours"))
        {
            using var publicHoursJson = JsonDocument.Parse(await publicHours.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.OK, publicHours.StatusCode);
            Assert.True(publicHoursJson.RootElement.GetProperty("isConfigured").GetBoolean());
            Assert.Equal(7, publicHoursJson.RootElement.GetProperty("days").GetArrayLength());
        }

        await RegisterAsync(client, "customer@example.test");
        var customerToken = await LoginAsync(client, "customer@example.test");
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Put, "api/account/phone", customerToken,
            JsonContent.Create(new { phoneNumber = "+52 55 1234 5678" }))).StatusCode);
        var requestJson = $$"""{"commandId":"{{Guid.NewGuid()}}","businessId":"{{businessId}}","zoneId":"{{zoneId}}","deliveryAddress":"Calle de prueba","items":[{"productId":"{{productId}}","quantity":2,"unitPrice":0.01}]}""";
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(client, HttpMethod.Post, "api/orders", customerToken,
            new StringContent(requestJson, Encoding.UTF8, "application/json"))).StatusCode);
        using var paymentDetailsResponse = await SendAsync(client, HttpMethod.Put,
            $"api/businesses/{businessId}/transfer-details", merchantToken,
            JsonContent.Create(new { recipientName = "Mercado Poraqui", institution = "Banco de prueba", accountReference = "CLABE-TEST-001", instructions = "Usa el número de pedido como concepto." }));
        Assert.Equal(HttpStatusCode.OK, paymentDetailsResponse.StatusCode);
        using var missingRequiredOption = await SendAsync(client, HttpMethod.Post, "api/orders", customerToken,
            JsonContent.Create(new { commandId = Guid.NewGuid(), businessId, zoneId, deliveryAddress = "Calle de prueba", items = new[] { new { productId, quantity = 2 } } }));
        Assert.Equal(HttpStatusCode.BadRequest, missingRequiredOption.StatusCode);
        using var unavailableOption = await SendAsync(client, HttpMethod.Post, "api/orders", customerToken,
            JsonContent.Create(new { commandId = Guid.NewGuid(), businessId, zoneId, deliveryAddress = "Calle de prueba", items = new[] { new { productId, quantity = 2, optionIds = new[] { largeOptionId, baconOptionId } } } }));
        Assert.Equal(HttpStatusCode.BadRequest, unavailableOption.StatusCode);
        using var tooManyRequiredChoices = await SendAsync(client, HttpMethod.Post, "api/orders", customerToken,
            JsonContent.Create(new { commandId = Guid.NewGuid(), businessId, zoneId, deliveryAddress = "Calle de prueba", items = new[] { new { productId, quantity = 2, optionIds = new[] { largeOptionId, regularOptionId } } } }));
        Assert.Equal(HttpStatusCode.BadRequest, tooManyRequiredChoices.StatusCode);
        var orderCommandId = Guid.NewGuid();
        var optionOrderRequest = $$"""{"commandId":"{{orderCommandId}}","businessId":"{{businessId}}","zoneId":"{{zoneId}}","deliveryAddress":"Calle de prueba","items":[{"productId":"{{productId}}","quantity":2,"optionIds":["{{largeOptionId}}","{{cheeseOptionId}}"]}]}""";
        using var orderResponse = await SendAsync(client, HttpMethod.Post, "api/orders", customerToken,
            new StringContent(optionOrderRequest, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Created, orderResponse.StatusCode);
        using var orderJson = JsonDocument.Parse(await orderResponse.Content.ReadAsStringAsync());
        Assert.Equal(76.50m, orderJson.RootElement.GetProperty("subtotal").GetDecimal());
        Assert.Equal(18.25m, orderJson.RootElement.GetProperty("deliveryFee").GetDecimal());
        Assert.Equal(94.75m, orderJson.RootElement.GetProperty("total").GetDecimal());
        Assert.Equal(2, orderJson.RootElement.GetProperty("items")[0].GetProperty("options").GetArrayLength());
        Assert.Equal(38.25m, orderJson.RootElement.GetProperty("items")[0].GetProperty("unitPrice").GetDecimal());
        Assert.Equal(76.50m, orderJson.RootElement.GetProperty("items")[0].GetProperty("lineTotal").GetDecimal());
        Assert.Equal("CLABE-TEST-001", orderJson.RootElement.GetProperty("transferInstructions").GetProperty("accountReference").GetString());
        var customerDeliveryCode = orderJson.RootElement.GetProperty("customerDeliveryCode").GetString()!;
        Assert.Equal(6, customerDeliveryCode.Length);
        var orderId = orderJson.RootElement.GetProperty("id").GetGuid();
        using (var replayedOrderResponse = await SendAsync(client, HttpMethod.Post, "api/orders", customerToken,
                   new StringContent(optionOrderRequest, Encoding.UTF8, "application/json")))
        using (var replayedOrderJson = JsonDocument.Parse(await replayedOrderResponse.Content.ReadAsStringAsync()))
        {
            Assert.Equal(HttpStatusCode.Created, replayedOrderResponse.StatusCode);
            Assert.Equal(orderId, replayedOrderJson.RootElement.GetProperty("id").GetGuid());
            Assert.Equal(customerDeliveryCode, replayedOrderJson.RootElement.GetProperty("customerDeliveryCode").GetString());
        }
        using (var conflictingOrderResponse = await SendAsync(client, HttpMethod.Post, "api/orders", customerToken,
                   JsonContent.Create(new { commandId = orderCommandId, businessId, zoneId, deliveryAddress = "Otra dirección",
                       items = new[] { new { productId, quantity = 2, optionIds = new[] { largeOptionId, cheeseOptionId } } } })))
            Assert.Equal(HttpStatusCode.Conflict, conflictingOrderResponse.StatusCode);
        using (var hoursResponse = await SendAsync(client, HttpMethod.Put,
                   $"api/businesses/{businessId}/opening-hours", merchantToken,
                   JsonContent.Create(new { timeZoneId = "UTC", days = Enum.GetValues<DayOfWeek>()
                       .Select(day => new { day = (int)day, isClosed = true, opensAt = (string?)null, closesAt = (string?)null }).ToArray() })))
            Assert.Equal(HttpStatusCode.OK, hoursResponse.StatusCode);
        using (var closedOrderResponse = await SendAsync(client, HttpMethod.Post, "api/orders", customerToken,
                   JsonContent.Create(new { commandId = Guid.NewGuid(), businessId, zoneId, deliveryAddress = "Calle de prueba", items = new[] { new { productId, quantity = 2, optionIds = new[] { largeOptionId, cheeseOptionId } } } })))
            Assert.Equal(HttpStatusCode.BadRequest, closedOrderResponse.StatusCode);
        using var realtimeSocket = await ConnectOrdersHubAsync(factory, client, customerToken, orderId);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsync("hubs/orders/negotiate?negotiateVersion=1", null)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var customer = await userManager.FindByEmailAsync("customer@example.test");
            var workflow = scope.ServiceProvider.GetRequiredService<OrderWorkflowService>();
            var snapshot = await workflow.GetRealtimeSnapshotAsync(orderId, customer!.Id, false, CancellationToken.None);
            Assert.Equal("PendingPayment", snapshot.Snapshot.Status);
            Assert.Equal(1, snapshot.Snapshot.Version);
            Assert.Equal("customer", snapshot.Audience);
        }
        using (var changedDetails = await SendAsync(client, HttpMethod.Put,
                   $"api/businesses/{businessId}/transfer-details", merchantToken,
                   JsonContent.Create(new { recipientName = "Mercado Poraqui", institution = "Banco de prueba", accountReference = "CLABE-TEST-CHANGED", instructions = "Nueva cuenta." })))
            Assert.Equal(HttpStatusCode.OK, changedDetails.StatusCode);

        var proofCommandId = Guid.NewGuid();
        using (var proofContent = new MultipartFormDataContent())
        {
            var proof = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.7 isolated transfer proof"));
            proof.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            proofContent.Add(new StringContent(proofCommandId.ToString()), "commandId");
            proofContent.Add(proof, "file", "receipt.pdf");
            using var proofRequest = new HttpRequestMessage(HttpMethod.Post, $"api/orders/{orderId}/transfer-evidence") { Content = proofContent };
            proofRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);
            using var proofResponse = await client.SendAsync(proofRequest);
            Assert.True(proofResponse.StatusCode == HttpStatusCode.Accepted,
                $"Evidence submission returned {(int)proofResponse.StatusCode}: {await proofResponse.Content.ReadAsStringAsync()}");
        }
        using (var replayContent = new MultipartFormDataContent())
        {
            var proof = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.7 isolated transfer proof"));
            proof.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            replayContent.Add(new StringContent(proofCommandId.ToString()), "commandId");
            replayContent.Add(proof, "file", "receipt.pdf");
            using var replayRequest = new HttpRequestMessage(HttpMethod.Post, $"api/orders/{orderId}/transfer-evidence") { Content = replayContent };
            replayRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);
            using var replayResponse = await client.SendAsync(replayRequest);
            Assert.Equal(HttpStatusCode.Accepted, replayResponse.StatusCode);
            using var replayJson = JsonDocument.Parse(await replayResponse.Content.ReadAsStringAsync());
            Assert.True(replayJson.RootElement.GetProperty("replayed").GetBoolean());
        }
        using (var conflictingContent = new MultipartFormDataContent())
        {
            var proof = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.7 different proof"));
            proof.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            conflictingContent.Add(new StringContent(proofCommandId.ToString()), "commandId");
            conflictingContent.Add(proof, "file", "receipt.pdf");
            using var conflictRequest = new HttpRequestMessage(HttpMethod.Post, $"api/orders/{orderId}/transfer-evidence") { Content = conflictingContent };
            conflictRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);
            using var conflictResponse = await client.SendAsync(conflictRequest);
            Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
        }
        using (var orderChanged = JsonDocument.Parse(await ReceiveSignalRFrameAsync(realtimeSocket)))
        {
            Assert.Equal(1, orderChanged.RootElement.GetProperty("type").GetInt32());
            Assert.Equal("OrderChanged", orderChanged.RootElement.GetProperty("target").GetString());
            Assert.Equal("AwaitingPaymentReview", orderChanged.RootElement.GetProperty("arguments")[0].GetProperty("status").GetString());
            Assert.Equal(2, orderChanged.RootElement.GetProperty("arguments")[0].GetProperty("version").GetInt64());
        }
        using (var persistedOrder = await SendAsync(client, HttpMethod.Get, $"api/orders/{orderId}", customerToken))
        {
            using var persistedJson = JsonDocument.Parse(await persistedOrder.Content.ReadAsStringAsync());
            Assert.Equal("AwaitingPaymentReview", persistedJson.RootElement.GetProperty("status").GetString());
        }
        using (var evidence = await SendAsync(client, HttpMethod.Get,
                   $"api/businesses/{businessId}/orders/{orderId}/transfer-evidence", merchantToken))
        {
            Assert.Equal(HttpStatusCode.OK, evidence.StatusCode);
            Assert.Equal("application/pdf", evidence.Content.Headers.ContentType?.MediaType);
            Assert.Equal("attachment", evidence.Content.Headers.ContentDisposition?.DispositionType);
        }
        await RegisterAsync(client, "other-merchant@example.test");
        var otherMerchantToken = await LoginAsync(client, "other-merchant@example.test");
        var otherBusinessId = await RegisterBusinessAsync(client, otherMerchantToken, "Otro negocio");
        otherMerchantToken = await LoginAsync(client, "other-merchant@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Get,
            $"api/businesses/{otherBusinessId}/orders/{orderId}/transfer-evidence", otherMerchantToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Put,
            $"api/option-groups/{optionGroupId}/availability", otherMerchantToken,
            JsonContent.Create(new { isAvailable = false }))).StatusCode);
        var incidentCommandId = Guid.NewGuid();
        var incidentRequest = new { commandId = incidentCommandId, category = "OrderQuestion", description = "El cliente solicita una actualización." };
        using var reportedIncidentResponse = await SendAsync(client, HttpMethod.Post, $"api/orders/{orderId}/incidents", customerToken,
            JsonContent.Create(incidentRequest));
        Assert.Equal(HttpStatusCode.OK, reportedIncidentResponse.StatusCode);
        using var reportedIncidentJson = JsonDocument.Parse(await reportedIncidentResponse.Content.ReadAsStringAsync());
        var incidentId = reportedIncidentJson.RootElement.GetProperty("id").GetGuid();
        using (var replayedIncidentResponse = await SendAsync(client, HttpMethod.Post, $"api/orders/{orderId}/incidents", customerToken,
                   JsonContent.Create(incidentRequest)))
        {
            using var replayedIncidentJson = JsonDocument.Parse(await replayedIncidentResponse.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.OK, replayedIncidentResponse.StatusCode);
            Assert.Equal(incidentId, replayedIncidentJson.RootElement.GetProperty("id").GetGuid());
        }
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(client, HttpMethod.Post, $"api/orders/{orderId}/incidents", customerToken,
            JsonContent.Create(new { commandId = incidentCommandId, category = "OrderQuestion", description = "Otro contenido" }))).StatusCode);
        using (var incidentList = await SendAsync(client, HttpMethod.Get, $"api/orders/{orderId}/incidents", customerToken))
        using (var incidentListJson = JsonDocument.Parse(await incidentList.Content.ReadAsStringAsync()))
            Assert.Single(incidentListJson.RootElement.EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Get,
            $"api/orders/{orderId}/incidents", otherMerchantToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Post,
            $"api/businesses/{businessId}/orders/{orderId}/incidents/{incidentId}/resolve", otherMerchantToken,
            JsonContent.Create(new { resolution = "Intento de otro negocio." }))).StatusCode);
        using (var resolvedIncident = await SendAsync(client, HttpMethod.Post,
                   $"api/businesses/{businessId}/orders/{orderId}/incidents/{incidentId}/resolve", merchantToken,
                   JsonContent.Create(new { resolution = "Se respondió al cliente." })))
        {
            using var resolvedJson = JsonDocument.Parse(await resolvedIncident.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.OK, resolvedIncident.StatusCode);
            Assert.Equal("Resolved", resolvedJson.RootElement.GetProperty("status").GetString());
        }
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Put,
            $"api/businesses/{businessId}/opening-hours", merchantToken, JsonContent.Create(alwaysOpenHours))).StatusCode);

        using var cancellableOrderResponse = await SendAsync(client, HttpMethod.Post, "api/orders", customerToken,
            new StringContent($$"""{"commandId":"{{Guid.NewGuid()}}","businessId":"{{businessId}}","zoneId":"{{zoneId}}","deliveryAddress":"Calle de prueba","items":[{"productId":"{{productId}}","quantity":2,"optionIds":["{{largeOptionId}}","{{cheeseOptionId}}"]}]}""", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, cancellableOrderResponse.StatusCode);
        using var cancellableOrderJson = JsonDocument.Parse(await cancellableOrderResponse.Content.ReadAsStringAsync());
        var cancellableOrderId = cancellableOrderJson.RootElement.GetProperty("id").GetGuid();
        using (var evidenceContent = new MultipartFormDataContent())
        {
            var proof = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.7 isolated refund proof"));
            proof.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            evidenceContent.Add(new StringContent(Guid.NewGuid().ToString()), "commandId");
            evidenceContent.Add(proof, "file", "refund-receipt.pdf");
            using var evidenceRequest = new HttpRequestMessage(HttpMethod.Post, $"api/orders/{cancellableOrderId}/transfer-evidence") { Content = evidenceContent };
            evidenceRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);
            using var evidenceResponse = await client.SendAsync(evidenceRequest);
            Assert.Equal(HttpStatusCode.Accepted, evidenceResponse.StatusCode);
        }
        using (var cancelledOrder = await SendAsync(client, HttpMethod.Post,
                   $"api/businesses/{businessId}/orders/{cancellableOrderId}/cancel", merchantToken,
                   JsonContent.Create(new { reason = "El negocio no podrá preparar el pedido." })))
        {
            using var cancelledJson = JsonDocument.Parse(await cancelledOrder.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.OK, cancelledOrder.StatusCode);
            Assert.Equal("Cancelled", cancelledJson.RootElement.GetProperty("status").GetString());
        }
        using (var customerCancelledOrder = await SendAsync(client, HttpMethod.Get,
                   $"api/orders/{cancellableOrderId}", customerToken))
        {
            using var customerCancelledJson = JsonDocument.Parse(await customerCancelledOrder.Content.ReadAsStringAsync());
            Assert.Equal("ReviewRequired", customerCancelledJson.RootElement.GetProperty("refundStatus").GetString());
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Post,
            $"api/orders/{cancellableOrderId}/cancel", otherMerchantToken,
            JsonContent.Create(new { reason = "Cancelación ajena" }))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Get,
            $"api/businesses/{businessId}/orders/{cancellableOrderId}/refund", otherMerchantToken)).StatusCode);
        using (var refundCase = await SendAsync(client, HttpMethod.Get,
                   $"api/businesses/{businessId}/orders/{cancellableOrderId}/refund", merchantToken))
        {
            using var refundJson = JsonDocument.Parse(await refundCase.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.OK, refundCase.StatusCode);
            Assert.Equal("ReviewRequired", refundJson.RootElement.GetProperty("status").GetString());
            var requestedRefund = refundJson.RootElement.GetProperty("requestedAmount").GetDecimal();
            var reviewRequest = JsonContent.Create(new { refunded = true, amount = requestedRefund,
                reference = "MANUAL-REFUND-TEST-001", note = "Reembolso realizado manualmente." });
            using var reviewResponse = await SendAsync(client, HttpMethod.Post,
                $"api/businesses/{businessId}/orders/{cancellableOrderId}/refund-review", merchantToken, reviewRequest);
            Assert.Equal(HttpStatusCode.OK, reviewResponse.StatusCode);
            using var reviewedRefund = JsonDocument.Parse(await reviewResponse.Content.ReadAsStringAsync());
            Assert.Equal("Refunded", reviewedRefund.RootElement.GetProperty("status").GetString());
        }
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var otherMerchant = await userManager.FindByEmailAsync("other-merchant@example.test");
            var workflow = scope.ServiceProvider.GetRequiredService<OrderWorkflowService>();
            await Assert.ThrowsAsync<OrderResourceForbiddenException>(() =>
                workflow.GetRealtimeSnapshotAsync(orderId, otherMerchant!.Id, false, CancellationToken.None));
        }

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post,
            $"api/businesses/{businessId}/orders/{orderId}/payment-review", merchantToken,
            JsonContent.Create(new { approved = true, note = "Transferencia localizada" }))).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var merchant = await userManager.FindByEmailAsync("merchant@example.test");
            var workflow = scope.ServiceProvider.GetRequiredService<OrderWorkflowService>();
            var snapshot = await workflow.GetRealtimeSnapshotAsync(orderId, merchant!.Id, false, CancellationToken.None);
            Assert.Equal("AwaitingBusinessDecision", snapshot.Snapshot.Status);
            Assert.Equal("business", snapshot.Audience);
        }
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post,
            $"api/businesses/{businessId}/orders/{orderId}/payment-review", merchantToken,
            JsonContent.Create(new { approved = true, note = "Transferencia localizada" }))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(client, HttpMethod.Post,
            $"api/businesses/{businessId}/orders/{orderId}/payment-review", merchantToken,
            JsonContent.Create(new { approved = true, note = "Nota distinta" }))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post,
            $"api/businesses/{businessId}/orders/{orderId}/decision", merchantToken,
            JsonContent.Create(new { accept = true, reason = "" }))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post,
            $"api/businesses/{businessId}/orders/{orderId}/decision", merchantToken,
            JsonContent.Create(new { accept = true, reason = "" }))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post,
            $"api/businesses/{businessId}/orders/{orderId}/preparation", merchantToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post,
            $"api/businesses/{businessId}/orders/{orderId}/ready", merchantToken)).StatusCode);

        await RegisterAsync(client, "courier@example.test");
        await RegisterAsync(client, "courier-two@example.test");
        Guid courierId;
        Guid courierTwoId;
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            courierId = (await userManager.FindByEmailAsync("courier@example.test"))!.Id;
            courierTwoId = (await userManager.FindByEmailAsync("courier-two@example.test"))!.Id;
        }
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Put,
            $"api/admin/users/{courierTwoId}/delivery-person", adminToken,
            JsonContent.Create(new { isDeliveryPerson = true }))).StatusCode);
        using var internalOrderResponse = await SendAsync(client, HttpMethod.Post, "api/orders", customerToken,
            JsonContent.Create(new { commandId = Guid.NewGuid(), businessId, zoneId, deliveryAddress = "Domicilio courier propio",
                items = new[] { new { productId, quantity = 1, optionIds = new[] { largeOptionId, cheeseOptionId } } } }));
        Assert.Equal(HttpStatusCode.Created, internalOrderResponse.StatusCode);
        using var internalOrderJson = JsonDocument.Parse(await internalOrderResponse.Content.ReadAsStringAsync());
        var internalOrderId = internalOrderJson.RootElement.GetProperty("id").GetGuid();
        using (var internalProofContent = new MultipartFormDataContent())
        {
            var proof = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.7 internal courier test"));
            proof.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            internalProofContent.Add(new StringContent(Guid.NewGuid().ToString()), "commandId");
            internalProofContent.Add(proof, "file", "internal-receipt.pdf");
            using var internalProofRequest = new HttpRequestMessage(HttpMethod.Post, $"api/orders/{internalOrderId}/transfer-evidence") { Content = internalProofContent };
            internalProofRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);
            using var internalProofResponse = await client.SendAsync(internalProofRequest);
            Assert.Equal(HttpStatusCode.Accepted, internalProofResponse.StatusCode);
        }
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post,
            $"api/businesses/{businessId}/orders/{internalOrderId}/payment-review", merchantToken,
            JsonContent.Create(new { approved = true, note = "Validación para courier propio" }))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post,
            $"api/businesses/{businessId}/orders/{internalOrderId}/decision", merchantToken,
            JsonContent.Create(new { accept = true, reason = "" }))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post,
            $"api/businesses/{businessId}/orders/{internalOrderId}/preparation", merchantToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post,
            $"api/businesses/{businessId}/orders/{internalOrderId}/ready", merchantToken)).StatusCode);
        var assignmentCommandId = Guid.NewGuid();
        using var assignmentResponse = await SendAsync(client, HttpMethod.Post,
            $"api/businesses/{businessId}/orders/{internalOrderId}/assign-courier", merchantToken,
            JsonContent.Create(new { courierUserId = courierTwoId, courierPaymentAmount = 37.50m, commandId = assignmentCommandId }));
        Assert.Equal(HttpStatusCode.OK, assignmentResponse.StatusCode);
        using var assignmentJson = JsonDocument.Parse(await assignmentResponse.Content.ReadAsStringAsync());
        var internalPickupCode = assignmentJson.RootElement.GetProperty("pickupCode").GetString();
        using (var assignmentReplay = await SendAsync(client, HttpMethod.Post,
                   $"api/businesses/{businessId}/orders/{internalOrderId}/assign-courier", merchantToken,
                   JsonContent.Create(new { courierUserId = courierTwoId, courierPaymentAmount = 37.50m, commandId = assignmentCommandId })))
        using (var replayJson = JsonDocument.Parse(await assignmentReplay.Content.ReadAsStringAsync()))
        {
            Assert.Equal(HttpStatusCode.OK, assignmentReplay.StatusCode);
            Assert.Equal(internalPickupCode, replayJson.RootElement.GetProperty("pickupCode").GetString());
        }
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(client, HttpMethod.Post,
            $"api/businesses/{businessId}/orders/{internalOrderId}/assign-courier", merchantToken,
            JsonContent.Create(new { courierUserId = courierTwoId, courierPaymentAmount = 38m, commandId = assignmentCommandId }))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(client, HttpMethod.Post,
            $"api/businesses/{businessId}/orders/{internalOrderId}/assign-courier", merchantToken,
            JsonContent.Create(new { courierUserId = courierTwoId, courierPaymentAmount = 37.50m, commandId = Guid.NewGuid() }))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Put,
            $"api/admin/users/{courierId}/external-courier", adminToken,
            JsonContent.Create(new { isExternalCourier = true }))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Put,
            $"api/admin/users/{courierTwoId}/external-courier", adminToken,
            JsonContent.Create(new { isExternalCourier = true }))).StatusCode);
        var courierToken = await LoginAsync(client, "courier@example.test");
        var courierTwoToken = await LoginAsync(client, "courier-two@example.test");
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Put, "api/courier/external/availability", courierToken,
            JsonContent.Create(new { isAvailable = true, zoneId }))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Put, "api/courier/external/availability", courierTwoToken,
            JsonContent.Create(new { isAvailable = true, zoneId }))).StatusCode);
        using (var dispatch = await SendAsync(client, HttpMethod.Post,
                   $"api/businesses/{businessId}/orders/{orderId}/external-offers", merchantToken,
                   JsonContent.Create(new { remuneration = 37.50m, conditions = "Pago en efectivo al recoger", expiresInMinutes = 15 })))
        {
            Assert.True(dispatch.StatusCode == HttpStatusCode.Accepted,
                $"External offer dispatch returned {(int)dispatch.StatusCode}: {await dispatch.Content.ReadAsStringAsync()}");
            using var dispatchJson = JsonDocument.Parse(await dispatch.Content.ReadAsStringAsync());
            Assert.Equal(2, dispatchJson.RootElement.GetProperty("offerCount").GetInt32());
        }
        using (var dispatchRetry = await SendAsync(client, HttpMethod.Post,
                   $"api/businesses/{businessId}/orders/{orderId}/external-offers", merchantToken,
                   JsonContent.Create(new { remuneration = 37.50m, conditions = "Pago en efectivo al recoger", expiresInMinutes = 15 })))
        {
            Assert.Equal(HttpStatusCode.Accepted, dispatchRetry.StatusCode);
            using var retryJson = JsonDocument.Parse(await dispatchRetry.Content.ReadAsStringAsync());
            Assert.Equal("OffersAlreadyActive", retryJson.RootElement.GetProperty("outcome").GetString());
            Assert.Equal(0, retryJson.RootElement.GetProperty("offerCount").GetInt32());
        }
        using var offersOneResponse = await SendAsync(client, HttpMethod.Get, "api/courier/external/offers", courierToken);
        using var offersTwoResponse = await SendAsync(client, HttpMethod.Get, "api/courier/external/offers", courierTwoToken);
        using var offersOne = JsonDocument.Parse(await offersOneResponse.Content.ReadAsStringAsync());
        using var offersTwo = JsonDocument.Parse(await offersTwoResponse.Content.ReadAsStringAsync());
        var offerOne = offersOne.RootElement[0];
        var offerTwo = offersTwo.RootElement[0];
        Assert.Equal("Domicilio prueba", offerOne.GetProperty("pickupAddress").GetString());
        Assert.Equal("Calle de prueba", offerOne.GetProperty("destinationAddress").GetString());
        Assert.Equal(37.50m, offerOne.GetProperty("remuneration").GetDecimal());
        Assert.Equal("Pago en efectivo al recoger", offerOne.GetProperty("conditions").GetString());
        using (var cancelled = await SendAsync(client, HttpMethod.Post,
                   $"api/businesses/{businessId}/orders/{orderId}/external-offers/cancel", merchantToken))
        {
            Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
            using var cancelledJson = JsonDocument.Parse(await cancelled.Content.ReadAsStringAsync());
            Assert.Equal(2, cancelledJson.RootElement.GetProperty("cancelledOffers").GetInt32());
        }
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Put, "api/courier/external/availability", courierToken,
            JsonContent.Create(new { isAvailable = false, zoneId = (Guid?)null }))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Put, "api/courier/external/availability", courierTwoToken,
            JsonContent.Create(new { isAvailable = false, zoneId = (Guid?)null }))).StatusCode);
        using (var noCouriers = await SendAsync(client, HttpMethod.Post,
                   $"api/businesses/{businessId}/orders/{orderId}/external-offers", merchantToken,
                   JsonContent.Create(new { remuneration = 37.50m, conditions = "Pago en efectivo al recoger", expiresInMinutes = 15 })))
        {
            Assert.Equal(HttpStatusCode.Accepted, noCouriers.StatusCode);
            using var noCourierJson = JsonDocument.Parse(await noCouriers.Content.ReadAsStringAsync());
            Assert.Equal("NoCouriersAvailable", noCourierJson.RootElement.GetProperty("outcome").GetString());
            Assert.Equal(0, noCourierJson.RootElement.GetProperty("offerCount").GetInt32());
        }
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Put, "api/courier/external/availability", courierToken,
            JsonContent.Create(new { isAvailable = true, zoneId }))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Put, "api/courier/external/availability", courierTwoToken,
            JsonContent.Create(new { isAvailable = true, zoneId }))).StatusCode);
        using (var redispatch = await SendAsync(client, HttpMethod.Post,
                   $"api/businesses/{businessId}/orders/{orderId}/external-offers", merchantToken,
                   JsonContent.Create(new { remuneration = 37.50m, conditions = "Pago en efectivo al recoger", expiresInMinutes = 15 })))
        {
            Assert.Equal(HttpStatusCode.Accepted, redispatch.StatusCode);
            using var redispatchJson = JsonDocument.Parse(await redispatch.Content.ReadAsStringAsync());
            Assert.Equal(2, redispatchJson.RootElement.GetProperty("offerCount").GetInt32());
        }
        using var refreshedOffersOneResponse = await SendAsync(client, HttpMethod.Get, "api/courier/external/offers", courierToken);
        using var refreshedOffersTwoResponse = await SendAsync(client, HttpMethod.Get, "api/courier/external/offers", courierTwoToken);
        using var refreshedOffersOne = JsonDocument.Parse(await refreshedOffersOneResponse.Content.ReadAsStringAsync());
        using var refreshedOffersTwo = JsonDocument.Parse(await refreshedOffersTwoResponse.Content.ReadAsStringAsync());
        var offerOneId = refreshedOffersOne.RootElement[0].GetProperty("id").GetGuid();
        var offerTwoId = refreshedOffersTwo.RootElement[0].GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post,
            $"api/courier/external/offers/{offerTwoId}/reject", courierTwoToken)).StatusCode);
        using var acceptResponse = await SendAsync(client, HttpMethod.Post,
            $"api/courier/external/offers/{offerOneId}/accept", courierToken);
        Assert.True(acceptResponse.StatusCode == HttpStatusCode.OK,
            $"Offer acceptance returned {(int)acceptResponse.StatusCode}: {await acceptResponse.Content.ReadAsStringAsync()}");
        using var assignment = JsonDocument.Parse(await acceptResponse.Content.ReadAsStringAsync());
        var pickupCode = assignment.RootElement.GetProperty("pickupCode").GetString()!;
        Assert.Equal(6, pickupCode.Length);
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(client, HttpMethod.Post,
            $"api/courier/external/offers/{offerTwoId}/accept", courierTwoToken)).StatusCode);

        using (var courierOrder = await SendAsync(client, HttpMethod.Get, $"api/courier/orders/{orderId}", courierToken))
        {
            Assert.Equal(HttpStatusCode.OK, courierOrder.StatusCode);
            using var courierOrderJson = JsonDocument.Parse(await courierOrder.Content.ReadAsStringAsync());
            Assert.Equal("Domicilio prueba", courierOrderJson.RootElement.GetProperty("businessPickupAddress").GetString());
            Assert.Equal(37.50m, courierOrderJson.RootElement.GetProperty("courierPaymentAmount").GetDecimal());
        }

        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(client, HttpMethod.Post,
            $"api/orders/{orderId}/pickup", courierToken, JsonContent.Create(new { code = "000000" }))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post,
            $"api/orders/{orderId}/pickup", courierToken, JsonContent.Create(new { code = pickupCode }))).StatusCode);
        using (var businessOrder = await SendAsync(client, HttpMethod.Get,
                   $"api/businesses/{businessId}/orders/{orderId}", merchantToken))
        {
            using var businessOrderJson = JsonDocument.Parse(await businessOrder.Content.ReadAsStringAsync());
            Assert.True(businessOrderJson.RootElement.GetProperty("courierPaymentReceived").GetBoolean());
            Assert.Equal(37.50m, businessOrderJson.RootElement.GetProperty("courierPaymentAmount").GetDecimal());
        }
        using (var contactResponse = await SendAsync(client, HttpMethod.Post,
                   $"api/orders/{orderId}/delivery/start", courierToken, JsonContent.Create(new { contactChannel = "whatsapp" })))
        {
            Assert.Equal(HttpStatusCode.OK, contactResponse.StatusCode);
            using var contact = JsonDocument.Parse(await contactResponse.Content.ReadAsStringAsync());
            Assert.Equal("525512345678", contact.RootElement.GetProperty("whatsAppDialString").GetString());
        }
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(client, HttpMethod.Post,
            $"api/orders/{orderId}/delivery/confirm", courierToken, JsonContent.Create(new { code = "111111" }))).StatusCode);
        var syncCommandId = Guid.NewGuid();
        var clientCapturedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        var offlineCommand = JsonContent.Create(new { commandId = syncCommandId, code = "111111", clientCapturedAtUtc = clientCapturedAt });
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(client, HttpMethod.Post,
            $"api/orders/{orderId}/sync/delivery-confirmation", courierToken, offlineCommand)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Post,
            $"api/orders/{orderId}/sync/delivery-confirmation", courierTwoToken,
            JsonContent.Create(new { commandId = Guid.NewGuid(), code = customerDeliveryCode, clientCapturedAtUtc = clientCapturedAt }))).StatusCode);
        using (var syncResponse = await SendAsync(client, HttpMethod.Post,
                   $"api/orders/{orderId}/sync/delivery-confirmation", courierToken,
                   JsonContent.Create(new { commandId = syncCommandId, code = customerDeliveryCode, clientCapturedAtUtc = clientCapturedAt })))
        {
            Assert.Equal(HttpStatusCode.OK, syncResponse.StatusCode);
            using var syncJson = JsonDocument.Parse(await syncResponse.Content.ReadAsStringAsync());
            Assert.False(syncJson.RootElement.GetProperty("replayed").GetBoolean());
        }
        using (var syncRetry = await SendAsync(client, HttpMethod.Post,
                   $"api/orders/{orderId}/sync/delivery-confirmation", courierToken,
                   JsonContent.Create(new { commandId = syncCommandId, code = customerDeliveryCode, clientCapturedAtUtc = clientCapturedAt })))
        {
            Assert.Equal(HttpStatusCode.OK, syncRetry.StatusCode);
            using var retryJson = JsonDocument.Parse(await syncRetry.Content.ReadAsStringAsync());
            Assert.True(retryJson.RootElement.GetProperty("replayed").GetBoolean());
        }
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(client, HttpMethod.Post,
            $"api/orders/{orderId}/sync/delivery-confirmation", courierToken,
            JsonContent.Create(new { commandId = syncCommandId, code = "111111", clientCapturedAtUtc = clientCapturedAt }))).StatusCode);

        using var customerOrderResponse = await SendAsync(client, HttpMethod.Get, $"api/orders/{orderId}", customerToken);
        using var customerOrder = JsonDocument.Parse(await customerOrderResponse.Content.ReadAsStringAsync());
        Assert.Equal("Delivered", customerOrder.RootElement.GetProperty("status").GetString());
        Assert.Equal(2, customerOrder.RootElement.GetProperty("items")[0].GetProperty("options").GetArrayLength());
        Assert.True(customerOrder.RootElement.GetProperty("courierPaymentAmount").ValueKind == JsonValueKind.Null);
        Assert.Equal("CLABE-TEST-001", customerOrder.RootElement.GetProperty("transferInstructions").GetProperty("accountReference").GetString());
        Assert.Equal(12, customerOrder.RootElement.GetProperty("history").GetArrayLength());
    }

    private static async Task RegisterAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("api/auth/register", new { email, password = "Test-Password-1!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("api/auth/login?useCookies=false", new { email, password = "Test-Password-1!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var content = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return content.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static async Task<Guid> RegisterBusinessAsync(HttpClient client, string token, string name)
    {
        using var response = await SendAsync(client, HttpMethod.Post, "api/businesses", token,
            JsonContent.Create(new { name, address = "Domicilio prueba" }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var content = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return content.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path,
        string token, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private static async Task<WebSocket> ConnectOrdersHubAsync(ApiTestHost factory, HttpClient client, string accessToken, Guid orderId)
    {
        var negotiateUrl = $"hubs/orders/negotiate?negotiateVersion=1&access_token={Uri.EscapeDataString(accessToken)}";
        using var negotiate = await client.PostAsync(negotiateUrl, null);
        Assert.Equal(HttpStatusCode.OK, negotiate.StatusCode);
        using var negotiateJson = JsonDocument.Parse(await negotiate.Content.ReadAsStringAsync());
        var connectionToken = negotiateJson.RootElement.GetProperty("connectionToken").GetString()!;
        var socketClient = factory.Server.CreateWebSocketClient();
        var socketUri = new Uri($"ws://localhost/hubs/orders?id={Uri.EscapeDataString(connectionToken)}&access_token={Uri.EscapeDataString(accessToken)}");
        var socket = await socketClient.ConnectAsync(socketUri, CancellationToken.None);
        await SendSignalRFrameAsync(socket, "{\"protocol\":\"json\",\"version\":1}");
        using (var handshake = JsonDocument.Parse(await ReceiveSignalRFrameAsync(socket)))
            Assert.False(handshake.RootElement.TryGetProperty("error", out _));
        await SendSignalRFrameAsync(socket,
            $"{{\"type\":1,\"invocationId\":\"subscribe-1\",\"target\":\"SubscribeOrder\",\"arguments\":[\"{orderId}\"]}}");
        using var subscription = JsonDocument.Parse(await ReceiveSignalRFrameAsync(socket));
        Assert.Equal("subscribe-1", subscription.RootElement.GetProperty("invocationId").GetString());
        Assert.Equal("PendingPayment", subscription.RootElement.GetProperty("result").GetProperty("status").GetString());
        return socket;
    }

    private static async Task SendSignalRFrameAsync(WebSocket socket, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json + "\u001e");
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static async Task<string> ReceiveSignalRFrameAsync(WebSocket socket)
    {
        var buffer = new byte[4096];
        using var message = new MemoryStream();
        while (true)
        {
            var received = await socket.ReceiveAsync(buffer, CancellationToken.None);
            if (received.MessageType == WebSocketMessageType.Close) throw new InvalidOperationException("SignalR socket closed before a frame arrived.");
            message.Write(buffer, 0, received.Count);
            var content = Encoding.UTF8.GetString(message.GetBuffer(), 0, checked((int)message.Length));
            var separator = content.IndexOf('\u001e');
            if (separator >= 0) return content[..separator];
        }
    }
}
