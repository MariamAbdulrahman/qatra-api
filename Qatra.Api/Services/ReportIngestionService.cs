using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Qatra.Api.Data;
using Qatra.Api.DTOs;
using Qatra.Api.Helpers;
using Qatra.Api.Interfaces;
using Qatra.Api.Models;
using Qatra.Api.Models.Enums;
using Qatra.Api.Options;

namespace Qatra.Api.Services;

public class ReportIngestionService : IReportIngestionService
{
    private readonly AppDbContext _db;
    private readonly ReportingOptions _options;

    public ReportIngestionService(AppDbContext db, IOptions<ReportingOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<IngestionResult> CreateAsync(CreateReportRequest request, CancellationToken ct = default)
    {
        // 1) التحقق من المدخلات: الزر السريع إجباري وله قيمتان فقط
        var quickButton = IssueTypeMapper.Parse(request.QuickButton);
        if (quickButton is null)
            return new IngestionResult(IngestionOutcome.InvalidQuickButton);

        var neighborhoodExists = await _db.Neighborhoods
            .AnyAsync(n => n.Id == request.NeighborhoodId, ct);
        if (!neighborhoodExists)
            return new IngestionResult(IngestionOutcome.NeighborhoodNotFound);

        // 2) الجهاز: Hash ثم البحث أو الإنشاء
        var device = await GetOrCreateDeviceAsync(request.DeviceKey, ct);

        // 3) الحد اليومي
        var startOfDay = DateTime.UtcNow.Date;
        var todayCount = await _db.Reports
            .CountAsync(r => r.DeviceId == device.Id && r.CreatedAt >= startOfDay, ct);

        if (todayCount >= _options.DailyReportLimit)
            return new IngestionResult(IngestionOutcome.DailyLimitExceeded);

        // 4) الحفظ بحالة Pending
        var report = new Report
        {
            DeviceId = device.Id,
            NeighborhoodId = request.NeighborhoodId,
            RawText = request.Text?.Trim() ?? string.Empty,
            QuickButton = quickButton,
            NeedsFollowup = true,
            ProcessingStatus = ProcessingStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _db.Reports.Add(report);
        await _db.SaveChangesAsync(ct);

        return new IngestionResult(IngestionOutcome.Created, report.Id, report.NeedsFollowup);
    }

    public async Task<FollowupOutcome> AddFollowupAsync(int reportId, FollowupRequest request, CancellationToken ct = default)
    {
        var report = await _db.Reports
            .Include(r => r.Device)
            .Include(r => r.Incident)
            .FirstOrDefaultAsync(r => r.Id == reportId, ct);

        if (report is null)
            return FollowupOutcome.NotFound;

        // لا يجيب إلا صاحب البلاغ نفسه
        if (report.Device.DeviceKeyHash != HashKey(request.DeviceKey))
            return FollowupOutcome.Forbidden;

        report.DurationDays = request.DurationDays;
        report.NeedsFollowup = false;

        if (report.Incident is not null &&
            (report.Incident.MaxDurationDays is null || request.DurationDays > report.Incident.MaxDurationDays))
        {
            report.Incident.MaxDurationDays = request.DurationDays;
        }

        await _db.SaveChangesAsync(ct);
        return FollowupOutcome.Updated;
    }

    private async Task<Device> GetOrCreateDeviceAsync(string deviceKey, CancellationToken ct)
    {
        var hash = HashKey(deviceKey);

        var device = await _db.Devices.FirstOrDefaultAsync(d => d.DeviceKeyHash == hash, ct);
        if (device is not null)
            return device;

        device = new Device { DeviceKeyHash = hash, CreatedAt = DateTime.UtcNow };
        _db.Devices.Add(device);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // طلبان متزامنان من جهاز جديد: الفهرس الفريد منع التكرار
            _db.Entry(device).State = EntityState.Detached;
            device = await _db.Devices.FirstAsync(d => d.DeviceKeyHash == hash, ct);
        }

        return device;
    }

    private static string HashKey(string deviceKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(deviceKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}