using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Qatra.Api.Interfaces;
using Qatra.Api.Models.Enums;

namespace Qatra.Api.Services;

public class AiExtractionService : IAiExtractionService
{
    private const string SystemPrompt =
        "أنت تستخرج بيانات من بلاغ مواطن عن المياه في مدينة يمنية، وقد يكتب باللهجة المحلية. " +
        "أرجع كائن JSON واحداً فقط بدون أي نص آخر وبدون علامات تنسيق، بهذه الحقول: " +
        "issueType: القيمة \"outage\" إذا كان البلاغ عن انقطاع المياه أو عدم وصولها أو فراغ الخزان، " +
        "والقيمة \"contaminated\" إذا كان عن تلوث المياه (لون أو رائحة أو طعم أو شوائب)، " +
        "والقيمة null إذا لم يكن البلاغ عن المياه أصلاً. " +
        "durationDays: عدد الأيام المبلَّغ عنها كعدد صحيح، والقيمة 0 إذا بدأت المشكلة اليوم (مثل «من الصبح»)، " +
        "والقيمة 1 إذا بدأت أمس، والقيمة null إذا لم تُذكر مدة. " +
        "eventDate: تاريخ بدء المشكلة بصيغة YYYY-MM-DD محسوباً من تاريخ اليوم المعطى لك، " +
        "والقيمة null إن لم يمكن تحديده. " +
        "confidence: رقم بين 0 و1 يعبّر عن ثقتك في التصنيف. " +
        "لا تخمّن اسم الحي ولا تضف حقولاً أخرى.";

    private readonly HttpClient _http;
    private readonly ILogger<AiExtractionService> _logger;
    private readonly string _model;
    private readonly string _apiKey;

    public AiExtractionService(HttpClient http, IConfiguration config, ILogger<AiExtractionService> logger)
    {
        _http = http;
        _logger = logger;
        _model = config["AiExtraction:Model"] ?? "";
        _apiKey = config["AiExtraction:ApiKey"] ?? "";
    }

    public async Task<ExtractionResult> ExtractAsync(string rawText, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return ExtractionResult.Fail("Empty text.");

        if (string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(_model))
            return ExtractionResult.Fail("Missing AiExtraction ApiKey or Model.");

        var today = DateTime.UtcNow.Date;
        var userText = $"تاريخ اليوم: {today:yyyy-MM-dd}\nنص البلاغ: {rawText.Trim()}";

        var body = new
        {
            systemInstruction = new { parts = new[] { new { text = SystemPrompt } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = userText } } } },
            generationConfig = new
            {
                temperature = 0.0,
                maxOutputTokens = 1024,
                responseMimeType = "application/json"
            }
        };

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"/v1beta/models/{_model}:generateContent")
            {
                Content = JsonContent.Create(body)
            };
            req.Headers.Add("x-goog-api-key", _apiKey);

            using var resp = await _http.SendAsync(req, ct);
            var status = (int)resp.StatusCode;

            if (!resp.IsSuccessStatusCode)
            {
                // مؤقت: حد الطلبات أو مهلة أو خطأ في الخادم
                var retryable = status == 429 || status == 408 || status >= 500;
                _logger.LogWarning("Extraction request returned {Status}.", status);
                return ExtractionResult.Fail($"HTTP {status}", retryable);
            }

            var raw = await resp.Content.ReadAsStringAsync(ct);
            var text = ExtractText(raw);
            if (text is null)
                return ExtractionResult.Fail("Empty or invalid model response.");

            return Parse(text, today);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested &&
                                   ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Extraction request failed.");
            return ExtractionResult.Fail("Network error or timeout.", retryable: true);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Extraction response was not valid JSON.");
            return ExtractionResult.Fail("Invalid JSON from model.");
        }
    }

    // ---------------------------------------------------------------- قراءة رد Gemini
    private static string? ExtractText(string raw)
    {
        using var doc = JsonDocument.Parse(raw);
        if (!doc.RootElement.TryGetProperty("candidates", out var cands) ||
            cands.ValueKind != JsonValueKind.Array || cands.GetArrayLength() == 0)
            return null;

        if (!cands[0].TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts) ||
            parts.ValueKind != JsonValueKind.Array)
            return null;

        var sb = new StringBuilder();
        foreach (var p in parts.EnumerateArray())
        {
            // نتجاهل أجزاء «التفكير» إن وُجدت
            if (p.TryGetProperty("thought", out var th) && th.ValueKind == JsonValueKind.True) continue;
            if (p.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                sb.Append(t.GetString());
        }
        var s = sb.ToString().Trim();
        return s.Length == 0 ? null : s;
    }

    // ---------------------------------------------------------------- تحليل JSON الاستخراج
    private static ExtractionResult Parse(string text, DateTime today)
    {
        // إزالة أسوار ```json إن أضافها النموذج
        text = text.Replace("```json", "").Replace("```", "").Trim();

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            return ExtractionResult.Fail("Model response is not a JSON object.");

        // 1) النوع: outage أو contaminated فقط
        if (!root.TryGetProperty("issueType", out var typeEl) || typeEl.ValueKind != JsonValueKind.String)
            return ExtractionResult.Fail("Message is not a water report.");

        var typeStr = typeEl.GetString()?.Trim();
        if (!Enum.TryParse<IssueType>(typeStr, ignoreCase: true, out var issueType) ||
            !Enum.IsDefined(typeof(IssueType), issueType) ||
            !(string.Equals(typeStr, "outage", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(typeStr, "contaminated", StringComparison.OrdinalIgnoreCase)))
            return ExtractionResult.Fail($"Unsupported issue type: {typeStr}");

        // 2) المدة
        int? duration = null;
        if (root.TryGetProperty("durationDays", out var durEl) && durEl.ValueKind == JsonValueKind.Number)
        {
            var d = (int)Math.Round(durEl.GetDouble());
            duration = Math.Clamp(d, 0, 365);
        }

        // 3) التاريخ (لا نقبل تاريخاً بعد اليوم)
        DateTime? eventDate = null;
        if (root.TryGetProperty("eventDate", out var dateEl) && dateEl.ValueKind == JsonValueKind.String &&
            DateTime.TryParse(dateEl.GetString(), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            eventDate = parsed.Date > today ? today : parsed.Date;
        }

        // 4) الثقة
        double confidence = 0;
        if (root.TryGetProperty("confidence", out var confEl) && confEl.ValueKind == JsonValueKind.Number)
            confidence = Math.Clamp(confEl.GetDouble(), 0, 1);

        return ExtractionResult.Ok(issueType, duration, eventDate, confidence);
    }
}