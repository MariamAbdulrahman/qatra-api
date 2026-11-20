using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Qatra.Api.Data;
using Qatra.Api.Interfaces;
using Qatra.Api.Models;
using Qatra.Api.Models.Enums;
using Qatra.Api.Options;

namespace Qatra.Api.Services;

public class IncidentMergingService : IIncidentMergingService
{
    private readonly AppDbContext _db;
    private readonly IncidentsOptions _opts;

    public IncidentMergingService(AppDbContext db, IOptions<IncidentsOptions> opts)
    {
        _db = db;
        _opts = opts.Value;
    }

    public async Task MergeAsync(Report report, CancellationToken ct = default)
    {
        // IssueType الفعلي: من الاستخراج أولاً، ثم الزر السريع كـ fallback
        var issueType = report.IssueType ?? report.QuickButton;
        if (issueType is null) return; // لا نستطيع التصنيف بدون نوع

        var mergeWindow = DateTime.UtcNow.AddHours(-_opts.MergeWindowHours);

        // البحث عن حادثة مفتوحة لنفس الحي والنوع ضمن نافذة الدمج
        var openStatuses = new[] { IncidentStatus.New, IncidentStatus.Confirmed, IncidentStatus.InProgress };

        var incident = await _db.Incidents
            .Where(i => i.NeighborhoodId == report.NeighborhoodId
                     && i.IssueType == issueType
                     && openStatuses.Contains(i.Status)
                     && i.LastReportAt >= mergeWindow)
            .OrderByDescending(i => i.LastReportAt)
            .FirstOrDefaultAsync(ct);

        if (incident is not null)
            await MergeIntoExistingAsync(incident, report, issueType.Value, ct);
        else
            await CreateNewIncidentAsync(report, issueType.Value, ct);
    }

    // ----------------------------------------------------------------
    // دمج في حادثة موجودة
    // ----------------------------------------------------------------
    private async Task MergeIntoExistingAsync(
        Incident incident, Report report, IssueType issueType, CancellationToken ct)
    {
        bool isNewDevice = !await _db.Reports
            .AnyAsync(r => r.IncidentId == incident.Id && r.DeviceId == report.DeviceId, ct);

        if (isNewDevice) incident.DistinctDevices++;

        incident.LastReportAt = DateTime.UtcNow;

        if (report.DurationDays.HasValue &&
            (incident.MaxDurationDays is null || report.DurationDays > incident.MaxDurationDays))
        {
            incident.MaxDurationDays = report.DurationDays;
        }

        incident.UrgentHealth = IsUrgentHealth(issueType, incident.DistinctDevices);

        report.IncidentId = incident.Id;
        await _db.SaveChangesAsync(ct);
    }

    // ----------------------------------------------------------------
    // إنشاء حادثة جديدة
    // ----------------------------------------------------------------
    private async Task CreateNewIncidentAsync(Report report, IssueType issueType, CancellationToken ct)
    {
        var repeatWindow = DateTime.UtcNow.AddHours(-_opts.RepeatWindowHours);

        bool isRepeat = await _db.Incidents
            .AnyAsync(i => i.NeighborhoodId == report.NeighborhoodId
                        && i.IssueType == issueType
                        && i.Status == IncidentStatus.Resolved
                        && i.ResolvedAt >= repeatWindow, ct);

        const int distinctDevices = 1;

        var incident = new Incident
        {
            NeighborhoodId = report.NeighborhoodId,
            IssueType = issueType,
            Status = IncidentStatus.New,
            DistinctDevices = distinctDevices,
            IsRepeat = isRepeat,
            UrgentHealth = IsUrgentHealth(issueType, distinctDevices),
            MaxDurationDays = report.DurationDays,
            LastReportAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _db.Incidents.Add(incident);
        await _db.SaveChangesAsync(ct); // للحصول على incident.Id

        report.IncidentId = incident.Id;
        await _db.SaveChangesAsync(ct);
    }

    // ----------------------------------------------------------------
    // UrgentHealth: contaminated + جهازان فأكثر
    // ----------------------------------------------------------------
    private static bool IsUrgentHealth(IssueType issueType, int distinctDevices)
        => issueType == IssueType.Contaminated && distinctDevices >= 2;
}
