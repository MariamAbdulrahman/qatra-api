using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Qatra.Api.Data;          // عدّلي إن كان AppDbContext في namespace آخر
using Qatra.Api.Interfaces;
using Qatra.Api.Models;

namespace Qatra.Api.Services;

public class PredictionService : IPredictionService
{
    private static readonly JsonSerializerOptions SnakeCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly HttpClient _http;
    private readonly AppDbContext _db;
    private readonly ILogger<PredictionService> _logger;
    private readonly string _predictPath;
    private readonly int _cacheMinutes;

    public PredictionService(
        HttpClient http,
        AppDbContext db,
        IConfiguration config,
        ILogger<PredictionService> logger)
    {
        _http = http;
        _db = db;
        _logger = logger;
        _predictPath = config["PredictionService:PredictPath"] ?? "/predict";
        _cacheMinutes = config.GetValue("PredictionService:CacheMinutes", 30);
    }

    // ==================================================================
    //  عقد خدمة Python (افتراض!). عدّلي هذا الكلاس فقط إن اختلف الطلب
    //  عمّا في https://qatra-ml-service.onrender.com/docs
    // ==================================================================
    private record MlRequest(
        int NeighborhoodId,
        double PriorCycleDays,
        double PriorCycleSd,
        List<double> IntervalsDays);   // فارغة حالياً: لا يوجد كشف لوصول الماء

    public async Task<PredictionResult?> GetPredictionAsync(int neighborhoodId, CancellationToken ct = default)
    {
        var hood = await _db.Neighborhoods.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == neighborhoodId, ct);
        if (hood is null) return null;

        // 1) من الكاش (جدول Predictions) لتجنب إيقاظ الخدمة النائمة على Render
        var cutoff = DateTime.UtcNow.AddMinutes(-_cacheMinutes);
        var cached = await _db.Predictions.AsNoTracking()
            .Where(p => p.NeighborhoodId == neighborhoodId && p.CreatedAt >= cutoff)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (cached is not null && TryParse(cached.Outputs, out var cachedDetails))
        {
            return new PredictionResult(neighborhoodId, "cache", cached.ModelVersion,
                cached.CreatedAt, cachedDetails);
        }

        // 2) من خدمة Python
        var request = new MlRequest(hood.Id, hood.PriorCycleDays, hood.PriorCycleSd, new List<double>());

        try
        {
            using var response = await _http.PostAsJsonAsync(_predictPath, request, SnakeCase, ct);
            response.EnsureSuccessStatusCode();

            var raw = await response.Content.ReadAsStringAsync(ct);
            if (!TryParse(raw, out var details))
                throw new JsonException("Empty or invalid JSON from prediction service.");

            var version = details.ValueKind == JsonValueKind.Object
                          && details.TryGetProperty("model_version", out var v)
                          && v.ValueKind == JsonValueKind.String
                ? v.GetString()!
                : "unknown";

            var entity = new Prediction
            {
                NeighborhoodId = hood.Id,
                ModelVersion = version,
                Inputs = JsonSerializer.Serialize(request, SnakeCase),
                Outputs = raw,
                CreatedAt = DateTime.UtcNow
            };
            _db.Predictions.Add(entity);
            await _db.SaveChangesAsync(ct);

            return new PredictionResult(hood.Id, "model", version, entity.CreatedAt, details);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested &&
                                   ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // 3) احتياطي: لا نخزّنه، ليُعاد المحاولة مع أول طلب لاحق
            _logger.LogWarning(ex, "Prediction service failed for neighborhood {Id}; using prior fallback.", hood.Id);

            var fallback = JsonSerializer.SerializeToElement(new
            {
                cycle_days = hood.PriorCycleDays,
                cycle_sd = hood.PriorCycleSd,
                note = "Estimated from the neighborhood's prior cycle only."
            });

            return new PredictionResult(hood.Id, "prior_fallback", "prior", DateTime.UtcNow, fallback);
        }
    }

    private static bool TryParse(string json, out JsonElement element)
    {
        element = default;
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            element = doc.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}