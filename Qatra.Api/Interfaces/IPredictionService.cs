using System.Text.Json;

namespace Qatra.Api.Interfaces;

public interface IPredictionService
{
    /// <summary>
    /// يرجع تنبؤ الحي، أو null إن لم يوجد الحي.
    /// لا يرمي استثناء عند تعطل خدمة Python: يرجع تنبؤاً احتياطياً من الدورة المسبقة.
    /// </summary>
    Task<PredictionResult?> GetPredictionAsync(int neighborhoodId, CancellationToken ct = default);
}

/// <param name="Source">"model" = من خدمة Python، "cache" = من جدول Predictions، "prior_fallback" = احتياطي</param>
/// <param name="Details">الرد الخام للخدمة كما هو (أو بيانات الدورة المسبقة في الاحتياطي)</param>
public record PredictionResult(
    int NeighborhoodId,
    string Source,
    string ModelVersion,
    DateTime GeneratedAtUtc,
    JsonElement Details);