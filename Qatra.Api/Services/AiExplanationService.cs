// namespace Qatra.Api.Services
// {
//     public class AiExplanationService
//     {
//     }
// }

using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Qatra.Api.Interfaces;
using Qatra.Api.Models.Enums;

namespace Qatra.Api.Services;

public class AiExplanationService : IAiExplanationService
{
    private const string SystemPrompt =
        "أنت مساعد توضيحي لنظام بلاغات المياه (قطرة). " +
        "لخّص حالة البلاغ في حي المستخدم بلغة عربية واضحة ومباشرة ومختصرة (جملتان إلى ثلاث) بناءً على المعطيات المرفقة، " +
        "دون اختلاق أرقام غير موجودة في المعطيات.";

    private readonly HttpClient _http;
    private readonly ILogger<AiExplanationService> _logger;
    private readonly string _model;
    private readonly string _apiKey;

    public AiExplanationService(HttpClient http, IConfiguration config, ILogger<AiExplanationService> logger)
    {
        _http = http;
        _logger = logger;
        _model = config["AiExplanation:Model"]
                 ?? config["AiExtraction:Model"]
                 ?? "gemini-3.5-flash-lite";
        _apiKey = config["AiExplanation:ApiKey"]
                  ?? config["AiExtraction:ApiKey"]
                  ?? "";
    }

    public async Task<ExplanationResult> ExplainAsync(ExplanationInput input, CancellationToken ct = default)
    {
        var fallbackText = BuildFallbackExplanation(input);

        if (string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(_model))
            return new ExplanationResult(fallbackText, false, "Missing AiExplanation ApiKey or Model.");

        var summaryPayload =
            $"الحي: {input.NeighborhoodNameAr}\n" +
            $"نوع المشكلة: {input.IssueType}\n" +
            $"عدد الأجهزة المبلّغة: {input.DistinctDevices}\n" +
            $"أقصى مدة بالأيام: {(input.MaxDurationDays.HasValue ? input.MaxDurationDays.Value.ToString() : "غير محدد")}\n" +
            $"بلاغ متكرر: {(input.IsRepeat ? "نعم" : "لا")}\n" +
            $"تنبيه صحي عاجل: {(input.UrgentHealth ? "نعم" : "لا")}\n" +
            $"الترتيب: {(input.Rank.HasValue ? input.Rank.Value.ToString() : "غير محدد")}\n" +
            $"معدل الدورة: {(input.CycleMean.HasValue ? input.CycleMean.Value.ToString("0.##") : "غير محدد")}\n" +
            $"احتمال وصول المياه خلال يومين: {(input.ProbArrival2d.HasValue ? input.ProbArrival2d.Value.ToString("P0") : "غير محدد")}\n" +
            $"تأخر غير طبيعي: {(input.AbnormalDelay.HasValue ? (input.AbnormalDelay.Value ? "نعم" : "لا") : "غير محدد")}\n" +
            $"أساس التنبؤ: {input.PredictionBasis ?? "غير محدد"}";

        var body = new
        {
            systemInstruction = new { parts = new[] { new { text = SystemPrompt } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = summaryPayload } } } },
            generationConfig = new { temperature = 0.2, maxOutputTokens = 300 }
        };

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"/v1beta/models/{_model}:generateContent")
            {
                Content = JsonContent.Create(body)
            };
            req.Headers.Add("x-goog-api-key", _apiKey);

            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var statusCode = (int)resp.StatusCode;
                _logger.LogWarning("Explanation request returned {Status}.", statusCode);
                return new ExplanationResult(fallbackText, false, $"HTTP {statusCode}");
            }

            var rawJson = await resp.Content.ReadAsStringAsync(ct);
            var text = ExtractModelText(rawJson);
            if (string.IsNullOrWhiteSpace(text))
                return new ExplanationResult(fallbackText, false, "Empty model response.");

            return new ExplanationResult(text.Trim(), true, null);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested &&
                                   ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Explanation request failed; returning fallback explanation.");
            return new ExplanationResult(fallbackText, false, "Network error or invalid response.");
        }
    }

    private static string BuildFallbackExplanation(ExplanationInput input)
    {
        var issueLabel = input.IssueType == IssueType.Contaminated ? "تلوث مياه" : "انقطاع مياه";
        var durationPart = input.MaxDurationDays.HasValue
            ? $" بمدة تصل إلى {input.MaxDurationDays.Value} يوم"
            : "";
        var healthPart = input.UrgentHealth ? " مع تنبيه صحي عاجل." : ".";
        return $"تم رصد {issueLabel} في {input.NeighborhoodNameAr} بناءً على {input.DistinctDevices} بلاغات{durationPart}{healthPart}";
    }

    private static string? ExtractModelText(string rawResponse)
    {
        using var doc = JsonDocument.Parse(rawResponse);
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0)
            return null;

        if (!candidates[0].TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts) ||
            parts.ValueKind != JsonValueKind.Array)
            return null;

        var sb = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True)
                continue;
            if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                sb.Append(text.GetString());
        }

        return sb.ToString();
    }
}