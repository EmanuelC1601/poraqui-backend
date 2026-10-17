using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Poraqui.Application.Security;
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

        using var rateResponse = await SendAsync(client, HttpMethod.Post, $"api/businesses/{businessId}/delivery-rates", merchantToken,
            JsonContent.Create(new { zoneId, fee = 18.25m }));
        Assert.Equal(HttpStatusCode.OK, rateResponse.StatusCode);

        await RegisterAsync(client, "customer@example.test");
        var customerToken = await LoginAsync(client, "customer@example.test");
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Put, "api/account/phone", customerToken,
            JsonContent.Create(new { phoneNumber = "+52 55 1234 5678" }))).StatusCode);
        var requestJson = $$"""{"businessId":"{{businessId}}","zoneId":"{{zoneId}}","deliveryAddress":"Calle de prueba","items":[{"productId":"{{productId}}","quantity":2,"unitPrice":0.01}]}""";
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(client, HttpMethod.Post, "api/orders", customerToken,
            new StringContent(requestJson, Encoding.UTF8, "application/json"))).StatusCode);
        using var paymentDetailsResponse = await SendAsync(client, HttpMethod.Put,
            $"api/businesses/{businessId}/transfer-details", merchantToken,
            JsonContent.Create(new { recipientName = "Mercado Poraqui", institution = "Banco de prueba", accountReference = "CLABE-TEST-001", instructions = "Usa el número de pedido como concepto." }));
        Assert.Equal(HttpStatusCode.OK, paymentDetailsResponse.StatusCode);
        using var orderResponse = await SendAsync(client, HttpMethod.Post, "api/orders", customerToken,
            new StringContent(requestJson, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Created, orderResponse.StatusCode);
        using var orderJson = JsonDocument.Parse(await orderResponse.Content.ReadAsStringAsync());
        Assert.Equal(51.00m, orderJson.RootElement.GetProperty("subtotal").GetDecimal());
        Assert.Equal(18.25m, orderJson.RootElement.GetProperty("deliveryFee").GetDecimal());
        Assert.Equal(69.25m, orderJson.RootElement.GetProperty("total").GetDecimal());
        Assert.Equal("CLABE-TEST-001", orderJson.RootElement.GetProperty("transferInstructions").GetProperty("accountReference").GetString());
        var customerDeliveryCode = orderJson.RootElement.GetProperty("customerDeliveryCode").GetString()!;
        Assert.Equal(6, customerDeliveryCode.Length);
        var orderId = orderJson.RootElement.GetProperty("id").GetGuid();
        using (var changedDetails = await SendAsync(client, HttpMethod.Put,
                   $"api/businesses/{businessId}/transfer-details", merchantToken,
                   JsonContent.Create(new { recipientName = "Mercado Poraqui", institution = "Banco de prueba", accountReference = "CLABE-TEST-CHANGED", instructions = "Nueva cuenta." })))
            Assert.Equal(HttpStatusCode.OK, changedDetails.StatusCode);

        using (var proofContent = new MultipartFormDataContent())
        {
            var proof = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.7 isolated transfer proof"));
            proof.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            proofContent.Add(proof, "file", "receipt.pdf");
            using var proofRequest = new HttpRequestMessage(HttpMethod.Post, $"api/orders/{orderId}/transfer-evidence") { Content = proofContent };
            proofRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);
            using var proofResponse = await client.SendAsync(proofRequest);
            Assert.True(proofResponse.StatusCode == HttpStatusCode.Accepted,
                $"Evidence submission returned {(int)proofResponse.StatusCode}: {await proofResponse.Content.ReadAsStringAsync()}");
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

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post,
            $"api/businesses/{businessId}/orders/{orderId}/payment-review", merchantToken,
            JsonContent.Create(new { approved = true, note = "Transferencia localizada" }))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(client, HttpMethod.Post,
            $"api/businesses/{businessId}/orders/{orderId}/payment-review", merchantToken,
            JsonContent.Create(new { approved = true, note = "Reintento" }))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post,
            $"api/businesses/{businessId}/orders/{orderId}/decision", merchantToken,
            JsonContent.Create(new { accept = true, reason = "" }))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(client, HttpMethod.Post,
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
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post,
            $"api/orders/{orderId}/delivery/confirm", courierToken, JsonContent.Create(new { code = customerDeliveryCode }))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(client, HttpMethod.Post,
            $"api/orders/{orderId}/delivery/confirm", courierToken, JsonContent.Create(new { code = customerDeliveryCode }))).StatusCode);

        using var customerOrderResponse = await SendAsync(client, HttpMethod.Get, $"api/orders/{orderId}", customerToken);
        using var customerOrder = JsonDocument.Parse(await customerOrderResponse.Content.ReadAsStringAsync());
        Assert.Equal("Delivered", customerOrder.RootElement.GetProperty("status").GetString());
        Assert.True(customerOrder.RootElement.GetProperty("courierPaymentAmount").ValueKind == JsonValueKind.Null);
        Assert.Equal("CLABE-TEST-001", customerOrder.RootElement.GetProperty("transferInstructions").GetProperty("accountReference").GetString());
        Assert.Equal(10, customerOrder.RootElement.GetProperty("history").GetArrayLength());
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
}
