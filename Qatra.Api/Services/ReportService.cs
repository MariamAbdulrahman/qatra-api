using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Qatra.Api.Data;
using Qatra.Api.DTOs;
using Qatra.Api.Interfaces;
using Qatra.Api.Models;
using Qatra.Api.Models.Enums;

namespace Qatra.Api.Services;

public class ReportService : IReportService
{
    private readonly AppDbContext _db;

    public ReportService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ReportCreatedDto?> CreateAsync(CreateReportDto dto)
    {
        var neighborhoodExists = await _db.Neighborhoods
            .AnyAsync(n => n.Id == dto.NeighborhoodId);

        if (!neighborhoodExists)
            return null;

        // نخزن بصمة الجهاز فقط وليس معرّفه الحقيقي (حفظًا للخصوصية)
        var keyHash = HashDeviceKey(dto.DeviceKey);

        var device = await _db.Devices
            .FirstOrDefaultAsync(d => d.DeviceKeyHash == keyHash);

        if (device is null)
        {
            device = new Device { DeviceKeyHash = keyHash };
            _db.Devices.Add(device);
        }

        var report = new Report
        {
            Device = device,
            NeighborhoodId = dto.NeighborhoodId,
            RawText = dto.Text?.Trim() ?? string.Empty,
            QuickButton = dto.QuickType,
            ProcessingStatus = ProcessingStatus.Pending
        };

        _db.Reports.Add(report);
        await _db.SaveChangesAsync();

        return new ReportCreatedDto
        {
            Id = report.Id,
            ProcessingStatus = report.ProcessingStatus,
            CreatedAt = report.CreatedAt
        };
    }

    public async Task<ReportStatusDto?> GetStatusAsync(int id)
    {
        var report = await _db.Reports
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new
            {
                r.Id,
                r.ProcessingStatus,
                r.NeedsFollowup,
                r.IncidentId
            })
            .FirstOrDefaultAsync();

        if (report is null)
            return null;

        // عدد البلاغات الأخرى المدموجة في الحالة نفسها
        var related = report.IncidentId is null
            ? 0
            : await _db.Reports.CountAsync(r =>
                r.IncidentId == report.IncidentId && r.Id != id);

        return new ReportStatusDto
        {
            Id = report.Id,
            ProcessingStatus = report.ProcessingStatus,
            NeedsFollowup = report.NeedsFollowup,
            RelatedReports = related
        };
    }

    private static string HashDeviceKey(string deviceKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(deviceKey.Trim()));
        return Convert.ToHexStringLower(bytes); // 64 حرفًا
    }
}