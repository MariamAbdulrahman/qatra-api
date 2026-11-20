using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Qatra.Api.Data;
using Qatra.Api.Models.Enums;
using Qatra.Api.Options;

namespace Qatra.Api.Workers;

/// <summary>
/// خدمة خلفية تعمل كل ساعة وتُغلق الحوادث الخاملة (لا بلاغ جديد منذ ExpirationHours).
/// </summary>
public class IncidentExpirationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IncidentExpirationWorker> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(1);

    public IncidentExpirationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<IncidentExpirationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IncidentExpirationWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpireStaleIncidentsAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "IncidentExpirationWorker encountered an error.");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task ExpireStaleIncidentsAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db   = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var opts = scope.ServiceProvider.GetRequiredService<IOptions<IncidentsOptions>>().Value;

        var cutoff = DateTime.UtcNow.AddHours(-opts.ExpirationHours);
        var openStatuses = new[] { IncidentStatus.New, IncidentStatus.Confirmed, IncidentStatus.InProgress };

        var stale = await db.Incidents
            .Where(i => openStatuses.Contains(i.Status) && i.LastReportAt < cutoff)
            .ToListAsync(ct);

        if (stale.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var incident in stale)
        {
            incident.Status = IncidentStatus.Expired;
            incident.ResolvedAt = now;
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Expired {Count} stale incident(s).", stale.Count);
    }
}
