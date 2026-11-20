
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace U360Prospect.Services;

public sealed class InactiveUserBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InactiveUserBackgroundService> _logger;

    public InactiveUserBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<InactiveUserBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.Now;
                var nextRun = now.Date.AddHours(2);

                if (nextRun <= now)
                    nextRun = nextRun.AddDays(1);

                var delay = nextRun - now;

                _logger.LogInformation(
                    "Next inactive-user check scheduled for {NextRun}.",
                    nextRun);

                await Task.Delay(delay, stoppingToken);

                using var scope = _scopeFactory.CreateScope();

                var authorization = scope.ServiceProvider
                    .GetRequiredService<AppUserAuthorizationService>();

                var disabledCount =
                    await authorization.DisableInactiveUsersAsync(
                        stoppingToken);

                _logger.LogInformation(
                    "Daily inactivity check completed. Accounts disabled: {Count}.",
                    disabledCount);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Daily inactive-user check failed.");

                // Avoid a rapid retry loop after an error.
                await Task.Delay(
                    TimeSpan.FromMinutes(5),
                    stoppingToken);
            }
        }
    }
}
