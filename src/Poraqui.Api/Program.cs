using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Poraqui.Infrastructure.Identity;
using Poraqui.Application.Accounts;
using Poraqui.Infrastructure.Persistence;
using Poraqui.Api.Security;
using Poraqui.Application.Commerce;
using Poraqui.Application.Orders;
using Poraqui.Infrastructure.Storage;
using Poraqui.Api.Realtime;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddScoped<IOrderChangePublisher, SignalROrderChangePublisher>();
builder.Services.AddHttpClient<IPhoneVerificationProvider, TwilioVerifyPhoneVerificationProvider>();
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var identityConfigured = !string.IsNullOrWhiteSpace(connectionString);
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
    builder.Services.AddScoped<IOrderWorkflowRepository, OrderWorkflowRepository>();
    builder.Services.AddScoped<OrderWorkflowService>();
    builder.Services.AddScoped<IExternalCourierRepository, ExternalCourierRepository>();
    builder.Services.AddScoped<ExternalCourierService>();
    builder.Services.AddSingleton<ITransferEvidenceStorage, FileTransferEvidenceStorage>();
}
builder.Services.AddPoraquiAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
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
    app.MapGroup("/api/auth").MapIdentityApi<ApplicationUser>();
    app.MapControllers();
    app.MapHub<OrdersHub>("/hubs/orders");
}

app.UseAuthorization();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.Run();

public partial class Program { }
