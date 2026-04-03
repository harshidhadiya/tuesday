using System.Diagnostics;
using Microsoft.EntityFrameworkCore; // Required for ToListAsync and RemoveRange
using AUCTION.Helpers;
using USER.Model;

namespace USER.Services
{
    public class TokenCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TokenCleanupService> _logger;
        
        // Settings for "Million Row" safety
        private const int BatchSize = 1000; 
        private const int DelayBetweenBatchesMs = 1000; 

        public TokenCleanupService(IServiceProvider serviceProvider, ILogger<TokenCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Refresh Token Cleanup Service is starting at {Time}", TimeHelper.Now());

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanExpiredTokensInBatches(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "CRITICAL: Error occurred while cleaning expired tokens.");
                }

                // Check every 6 hours
                _logger.LogInformation("Batch cycle complete. Next check in 6 hours...");
                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
        }

        public async Task CleanExpiredTokensInBatches(CancellationToken stoppingToken)
        {
            bool hasMore = true;
            int totalDeleted = 0;

            while (hasMore && !stoppingToken.IsCancellationRequested)
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<MACUTIONDB>();
                    var sw = Stopwatch.StartNew();

                    // 1. Fetch only the data needed for deletion. 
                    // Using AsNoTracking() saves massive amounts of memory.
                    var expiredBatch = await db.refreshTables
                        .Where(t => t.expiryDate < TimeHelper.Now())
                        .Take(BatchSize)
                        .AsNoTracking() 
                        .ToListAsync(stoppingToken);

                    if (expiredBatch.Any())
                    {
                        // 2. Remove the batch
                        db.refreshTables.RemoveRange(expiredBatch);
                        await db.SaveChangesAsync(stoppingToken);
                        
                        sw.Stop();
                        totalDeleted += expiredBatch.Count;

                        _logger.LogInformation("Successfully deleted {Count} expired tokens. [Time: {Ms}ms] [Total: {Total}]", 
                            expiredBatch.Count, sw.ElapsedMilliseconds, totalDeleted);

                        // 3. Give the DB a 1-second breather to handle other user requests (Login/Refresh)
                        await Task.Delay(DelayBetweenBatchesMs, stoppingToken);
                    }
                    else
                    {
                        hasMore = false;
                        _logger.LogInformation("Cleanup finished. Total tokens removed from database: {Total}", totalDeleted);
                    }
                }
            }
        }
    }
}