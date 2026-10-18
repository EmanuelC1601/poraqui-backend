using System.Net;
using System.Text.Json;

namespace Poraqui.Domain.Tests;

public sealed class OpenApiContractTests
{
    [Fact]
    public async Task Transfer_evidence_contract_describes_multipart_upload_and_actual_responses()
    {
        await using var factory = new ApiTestHost();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var operation = document.RootElement.GetProperty("paths")
            .GetProperty("/api/orders/{orderId}/transfer-evidence")
            .GetProperty("post");

        var requestBody = operation.GetProperty("requestBody");
        Assert.True(requestBody.GetProperty("required").GetBoolean());
        var content = requestBody.GetProperty("content");
        Assert.True(content.TryGetProperty("multipart/form-data", out var multipart));
        Assert.False(content.TryGetProperty("application/x-www-form-urlencoded", out _));

        var schema = multipart.GetProperty("schema");
        var properties = schema.GetProperty("properties");
        Assert.Equal("uuid", properties.GetProperty("commandId").GetProperty("format").GetString());
        Assert.Equal("binary", properties.GetProperty("file").GetProperty("format").GetString());
        var required = schema.GetProperty("required").EnumerateArray().Select(value => value.GetString()).ToArray();
        Assert.Contains("commandId", required);
        Assert.Contains("file", required);

        var responses = operation.GetProperty("responses");
        Assert.True(responses.TryGetProperty("202", out _));
        var acceptedSchema = responses.GetProperty("202").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema");
        Assert.Equal("boolean", acceptedSchema.GetProperty("properties").GetProperty("replayed").GetProperty("type").GetString());
        Assert.Contains("replayed", acceptedSchema.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.True(responses.TryGetProperty("409", out _));
        Assert.True(responses.TryGetProperty("413", out _));
        Assert.False(responses.TryGetProperty("200", out _));
    }

    [Fact]
    public async Task Customer_cancellation_contract_describes_order_refund_status_response()
    {
        await using var factory = new ApiTestHost();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var operation = document.RootElement.GetProperty("paths")
            .GetProperty("/api/orders/{orderId}/cancel")
            .GetProperty("post");

        Assert.Equal("Cancel an order before business acceptance", operation.GetProperty("summary").GetString());
        Assert.Contains("manual refund", operation.GetProperty("description").GetString(), StringComparison.OrdinalIgnoreCase);
        var responses = operation.GetProperty("responses");
        Assert.True(responses.TryGetProperty("200", out var success));
        Assert.True(responses.TryGetProperty("409", out _));
        Assert.False(responses.GetProperty("401").TryGetProperty("content", out _));
        Assert.Equal("string", responses.GetProperty("400").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema").GetProperty("properties")
            .GetProperty("error").GetProperty("type").GetString());
        var schemaRef = success.GetProperty("content").GetProperty("application/json")
            .GetProperty("schema").GetProperty("$ref").GetString();
        var schemaName = schemaRef!.Split('/').Last();
        var schema = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(schemaName);
        var refundStatusType = schema.GetProperty("properties").GetProperty("refundStatus").GetProperty("type");
        Assert.Contains("string", refundStatusType.EnumerateArray().Select(type => type.GetString()));

        var snapshotPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../docs/openapi.json"));
        using var snapshot = JsonDocument.Parse(await File.ReadAllTextAsync(snapshotPath));
        var snapshotOperation = snapshot.RootElement.GetProperty("paths")
            .GetProperty("/api/orders/{orderId}/cancel").GetProperty("post");
        Assert.Equal(operation.GetProperty("summary").GetString(), snapshotOperation.GetProperty("summary").GetString());
        var snapshotResponseSchema = snapshotOperation.GetProperty("responses").GetProperty("200")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString()!;
        Assert.EndsWith(schemaName, snapshotResponseSchema, StringComparison.Ordinal);
        Assert.True(snapshot.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty(schemaName).GetProperty("properties").TryGetProperty("refundStatus", out _));
    }
}
