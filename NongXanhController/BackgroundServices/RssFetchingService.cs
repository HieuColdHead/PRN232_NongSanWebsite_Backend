using System;
using System.Threading;
using System.Threading.Tasks;
using BLL.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace NongXanhController.BackgroundServices
{
    public class RssFetchingService : IHostedService, IDisposable
    {
        private readonly ILogger<RssFetchingService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private Timer? _timer;

        public RssFetchingService(
            ILogger<RssFetchingService> logger,
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _configuration = configuration;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("RSS Fetching Hosted Service is starting.");

            var intervalMinutes = _configuration.GetValue<int>("RssSettings:FetchIntervalMinutes", 60);
            _logger.LogInformation("RSS fetch will run every {Minutes} minutes.", intervalMinutes);

            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromMinutes(intervalMinutes));

            return Task.CompletedTask;
        }

        private async void DoWork(object? state)
        {
            _logger.LogInformation("RSS Fetching Hosted Service is working.");

            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var rssService = scope.ServiceProvider.GetRequiredService<IArticleRssService>();
                    var articlesSaved = await rssService.FetchAndSaveArticlesAsync();
                    if (articlesSaved > 0)
                    {
                        _logger.LogInformation("RSS Fetching Task completed, {Count} new articles were saved.", articlesSaved);
                    }
                    else
                    {
                        _logger.LogInformation("RSS Fetching Task completed, no new articles found.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching RSS feeds.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("RSS Fetching Hosted Service is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
