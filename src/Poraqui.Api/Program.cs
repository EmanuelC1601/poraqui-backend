using Microsoft.AspNetCore.Identity;
using Microsoft.OpenApi;
using System.Text.Json.Schema;
using Microsoft.EntityFrameworkCore;
using Poraqui.Infrastructure.Identity;
using Poraqui.Infrastructure.Persistence;
using Poraqui.Api.Security;
using Poraqui.Application.Commerce;
using Poraqui.Application.Orders;
using Poraqui.Infrastructure.Storage;
using Poraqui.Api.Realtime;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Poraqui.Application.Accounts;
using System.Threading.RateLimiting;
using Poraqui.Api.HostedServices;
using Poraqui.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi(options => options.AddOperationTransformer((operation, context, _) =>
{
    if (context.Description.HttpMethod == "POST" &&
        context.Description.RelativePath?.TrimStart('/').EndsWith("/transfer-evidence", StringComparison.Ordinal) == true)
    {
        operation.Summary = "Upload private transfer evidence";
        operation.Description = "Uploads a JPEG, PNG, or PDF transfer receipt. Files larger than 8 MB are rejected.";
        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new()
                {
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["commandId"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
                            ["file"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" }
                        },
                        Required = new HashSet<string>(StringComparer.Ordinal) { "commandId", "file" }
                    }
                }
            }
        };
        operation.Responses ??= new OpenApiResponses();
        operation.Responses.Remove("200");
        operation.Responses["202"] = new OpenApiResponse
        {
            Description = "Evidence accepted or an identical command replayed.",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new()
                {
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["replayed"] = new OpenApiSchema { Type = JsonSchemaType.Boolean }
                        },
                        Required = new HashSet<string>(StringComparer.Ordinal) { "replayed" }
                    }
                }
            }
        };
        operation.Responses["400"] = new OpenApiResponse { Description = "Invalid command or unsupported evidence file." };
        operation.Responses["401"] = new OpenApiResponse { Description = "Authentication is required." };
        operation.Responses["403"] = new OpenApiResponse { Description = "The order belongs to another customer." };
        operation.Responses["404"] = new OpenApiResponse { Description = "Order not found." };
        operation.Responses["409"] = new OpenApiResponse { Description = "Command conflict or invalid order state." };
        operation.Responses["413"] = new OpenApiResponse { Description = "Evidence exceeds the request size limit." };
    }
    else if (context.Description.HttpMethod == "POST" &&
             string.Equals(context.Description.RelativePath?.TrimStart('/'), "api/orders/{orderId}/cancel", StringComparison.Ordinal))
    {
        operation.Summary = "Cancel an order before business acceptance";
        operation.Description = "The customer may cancel while payment is pending review or the business has not accepted. If transfer evidence was submitted or approved, the response includes the manual refund follow-up status. Cancellation is not allowed after business acceptance.";
        operation.Responses ??= new OpenApiResponses();
        operation.Responses["400"] = new OpenApiResponse
        {
            Description = "Cancellation request is invalid.",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new()
                {
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["error"] = new OpenApiSchema { Type = JsonSchemaType.String }
                        },
                        Required = new HashSet<string>(StringComparer.Ordinal) { "error" }
                    }
                }
            }
        };
        operation.Responses["401"] = new OpenApiResponse { Description = "Authentication is required." };
        operation.Responses["403"] = new OpenApiResponse { Description = "The order belongs to another customer." };
        operation.Responses["404"] = new OpenApiResponse { Description = "Order not found." };
        operation.Responses["409"] = new OpenApiResponse
        {
            Description = "The order state changed or cancellation is not allowed.",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new()
                {
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["error"] = new OpenApiSchema { Type = JsonSchemaType.String }
                        },
                        Required = new HashSet<string>(StringComparer.Ordinal) { "error" }
                    }
                }
            }
        };
    }

    return Task.CompletedTask;
}));
builder.Services.AddHttpClient<IPhoneVerificationProvider, TwilioVerifyPhoneVerificationProvider>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("PhoneVerification", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(10),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});
builder.Services.AddSignalR();
builder.Services.AddScoped<IOrderChangePublisher, SignalROrderChangePublisher>();
builder.Services.AddScoped<IExternalOfferPublisher, SignalRExternalOfferPublisher>();
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var identityConfigured = !string.IsNullOrWhiteSpace(connectionString);
var webRootPath = builder.Environment.WebRootPath ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("Poraqui");
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (identityConfigured && builder.Environment.IsProduction())
    dataProtectionKeysPath = PersistentDataProtectionConfiguration.ValidateProductionPath(dataProtectionKeysPath, webRootPath);

if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    Directory.CreateDirectory(dataProtectionKeysPath);
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}
if (identityConfigured && builder.Environment.IsProduction())
{
    PrivateEvidenceStorageConfiguration.ValidateProductionRoot(builder.Configuration["PrivateFiles:Root"], webRootPath);
}

if (!string.IsNullOrWhiteSpace(connectionString))
{
    if (!builder.Environment.IsEnvironment("Testing"))
        builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
    builder.Services.AddIdentityApiEndpoints<ApplicationUser>()
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<AppDbContext>();
    builder.Services.AddScoped<IBusinessRepository, BusinessRepository>();
    builder.Services.AddScoped<BusinessService>();
    builder.Services.AddScoped<ICatalogOrderRepository, CatalogOrderRepository>();
    builder.Services.AddScoped<CatalogOrderService>();
    builder.Services.AddScoped<IOrderCommandSecretProtector, DataProtectionOrderCommandSecretProtector>();
    builder.Services.AddScoped<IExternalOfferSecretProtector>(provider =>
        (IExternalOfferSecretProtector)provider.GetRequiredService<IOrderCommandSecretProtector>());
    builder.Services.AddScoped<IOrderWorkflowRepository, OrderWorkflowRepository>();
    builder.Services.AddScoped<OrderWorkflowService>();
    builder.Services.AddScoped<IExternalCourierRepository, ExternalCourierRepository>();
    builder.Services.AddScoped<ExternalCourierService>();
    if (!builder.Environment.IsEnvironment("Testing"))
        builder.Services.AddHostedService<ExternalOfferExpirationWorker>();
    builder.Services.AddSingleton<ITransferEvidenceStorage, FileTransferEvidenceStorage>();
}
builder.Services.AddPoraquiAuthorization();

var app = builder.Build();

if (identityConfigured && app.Environment.IsProduction())
    app.Services.GetRequiredService<ITransferEvidenceStorage>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapOpenApi();
}

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/hubs/orders") &&
        string.IsNullOrEmpty(context.Request.Headers.Authorization) &&
        !string.IsNullOrEmpty(context.Request.Query["access_token"]))
        context.Request.Headers.Authorization = $"Bearer {context.Request.Query["access_token"]}";
    await next();
});

app.UseHttpsRedirection();

if (identityConfigured)
{
    app.UseAuthentication();
    app.UseRateLimiter();
    app.MapGroup("/api/auth").MapIdentityApi<ApplicationUser>();
    app.MapControllers();
    app.MapHub<OrdersHub>("/hubs/orders");
}

app.UseAuthorization();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.Run();

public partial class Program { }
