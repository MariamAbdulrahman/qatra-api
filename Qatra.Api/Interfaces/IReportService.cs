using Qatra.Api.DTOs;

namespace Qatra.Api.Interfaces;

public interface IReportService
{
    // يعيد null إذا كان الحي غير موجود
    Task<ReportCreatedDto?> CreateAsync(CreateReportDto dto);

    // يعيد null إذا لم يوجد البلاغ
    Task<ReportStatusDto?> GetStatusAsync(int id);
}