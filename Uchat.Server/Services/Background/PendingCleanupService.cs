using Uchat.Database.Repositories.Interfaces;

namespace Uchat.Server.Services.Background;

public class PendingCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PendingCleanupService> _logger;

    public PendingCleanupService(IServiceProvider serviceProvider, ILogger<PendingCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Pending Cleanup Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var pendingRepo = scope.ServiceProvider.GetRequiredService<IPendingRegistrationRepository>();

                    int deletedCount = await pendingRepo.DeleteExpiredAsync();
                    
                    if (deletedCount > 0)
                    {
                        _logger.LogInformation($"Cleaned up {deletedCount} expired registration requests.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during pending registration cleanup.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}