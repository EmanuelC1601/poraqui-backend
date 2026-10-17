using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Poraqui.Application.Orders;

namespace Poraqui.Api.HostedServices;

public sealed class ExternalOfferExpirationWorker(IServiceScopeFactory scopeFactory, ILogger<ExternalOfferExpirationWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<ExternalCourierService>();
                var expired = await service.ExpireDueOffersAsync(stoppingToken);
                if (expired > 0) logger.LogInformation("Expired {OfferCount} external delivery offers", expired);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (DbUpdateConcurrencyException)
            {
                logger.LogDebug("An external delivery offer changed concurrently with its expiration sweep");
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not expire due external delivery offers");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
