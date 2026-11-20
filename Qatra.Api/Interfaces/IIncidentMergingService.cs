using Qatra.Api.Models;

namespace Qatra.Api.Interfaces;

public interface IIncidentMergingService
{
    /// <summary>
    /// يدمج البلاغ في حادثة مفتوحة موجودة أو ينشئ حادثة جديدة.
    /// يُستدعى من ReportProcessingWorker بعد انتهاء الاستخراج (ProcessingStatus == Processed).
    /// يمكن استدعاؤه مباشرة في الاختبارات بشكل مستقل.
    /// </summary>
    Task MergeAsync(Report report, CancellationToken ct = default);
}
