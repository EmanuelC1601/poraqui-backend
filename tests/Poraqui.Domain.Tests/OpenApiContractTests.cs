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
}
