using Microsoft.EntityFrameworkCore;
using MoviesAPI.Data;

namespace MoviesAPI.Services;

public class LetterboxdPollingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LetterboxdPollingService> _logger;
    private readonly int _pollHours;

    public LetterboxdPollingService(IServiceScopeFactory scopeFactory, ILogger<LetterboxdPollingService> logger, IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _pollHours = int.TryParse(Environment.GetEnvironmentVariable("LETTERBOXD_POLL_HOURS"), out var h) ? h
            : configuration.GetValue<int>("Letterboxd:PollHours", 6);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LetterboxdPollingService started, polling every {Hours}h", _pollHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            await SyncAllUsersAsync();
            await Task.Delay(TimeSpan.FromHours(_pollHours), stoppingToken);
        }
    }

    private async Task SyncAllUsersAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var letterboxdService = scope.ServiceProvider.GetRequiredService<LetterboxdService>();

        var usersWithLetterboxd = await context.Auth
            .Where(u => u.LetterboxdUsername != null)
            .Select(u => u.Id)
            .ToListAsync();

        _logger.LogInformation("Polling Letterboxd for {Count} users", usersWithLetterboxd.Count);

        foreach (var userId in usersWithLetterboxd)
        {
            try
            {
                await letterboxdService.SyncUserAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing Letterboxd for user {UserId}", userId);
            }
        }
    }
}
