using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Qatra.Api.Interfaces;
using Qatra.Api.Models.Enums;

namespace Qatra.Api.Services;

public class AiExplanationService : IAiExplanationService
{
    private const string SystemPrompt =
        "أنت تكتب تبريراً قصيراً لمدير مؤسسة المياه عن حالة انقطاع أو تلوث في أحد الأحياء. " +
        "اكتب جملتين فقط بالعربية الفصحى المبسطة: الأولى تشرح سبب أولوية الحي بحسب المعطيات، والثانية تلخص التنبؤ. " +
        "استخدم فقط المعلومات والأرقام الواردة في المعطيات، ولا تضف رقماً أو سبباً أو توصية من عندك. " +
        "إن لم يتوفر تنبؤ فقل ذلك في الجملة الثانية. " +
        "إن ذكرت المعطيات أن التنبؤ مبني على الدورة المسبقة فقط فاذكر أنه تقدير تقريبي. " +
        "اكتب الأرقام بالأرقام الغربية (0-9). لا تستخدم عناوين ولا قوائم ولا علامات تنسيق.";

    private static readonly Regex NumberRx = new(@"\d+(?:\.\d+)?", RegexOptions.Compiled);

    private readonly HttpClient _http;
    private readonly ILogger<AiExplanationService> _logger;
    private readonly string _model;
    private readonly string _apiKey;

    public AiExplanationService(HttpClient http, IConfiguration config, ILogger<AiExplanationService> logger)
    {
        _http = http;
        _logger = logger;
        _model = config["AiExplanation:Model"] ?? config["AiExtraction:Model"] ?? "";
        // نفس مفتاح الاستخراج من User Secrets ما لم تضعي مفتاحاً خاصاً
        _apiKey = config["AiExplanation:ApiKey"] ?? config["AiExtraction:ApiKey"] ?? "";
    }

    public async Task<ExplanationResult> ExplainAsync(ExplanationInput input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(_model))
            return Fallback(input, "Missing ApiKey or Model.");

        var prompt = BuildPrompt(input);

        var body = new
        {
            systemInstruction = new { parts = new[] { new { text = SystemPrompt } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = prompt } } } },
            generationConfig = new { temperature = 0.2, maxOutputTokens = 1024 }
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
                _logger.LogWarning("Explanation request returned {Status}.", (int)resp.StatusCode);
                return Fallback(input, $"HTTP {(int)resp.StatusCode}");
            }

            var raw = await resp.Content.ReadAsStringAsync(ct);
            var text = ExtractText(raw);
            if (text is null)
                return Fallback(input, "Empty or invalid model response.");

            text = Clean(text);
            if (text.Length < 20 || text.Length > 600)
                return Fallback(input, "Unexpected response length.");

            if (!NumbersAreGrounded(text, prompt))
                return Fallback(input, "Model used a number that is not in the input.");

            return new ExplanationResult(text, true, null);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested &&
                                   ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Explanation request failed.");
            return Fallback(input, "Network error or timeout.");
        }
    }

    // ---------------------------------------------------------------- المعطيات
    private static string BuildPrompt(ExplanationInput i)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"الحي: {i.NeighborhoodNameAr}");
        sb.AppendLine($"نوع المشكلة: {IssueLabel(i.IssueType)}");
        sb.AppendLine($"عدد الأجهزة المبلِّغة: {i.DistinctDevices}");
        if (i.MaxDurationDays is not null)
            sb.AppendLine($"أطول مدة مبلَّغ عنها بالأيام: {i.MaxDurationDays}");
        if (i.Rank is not null)
            sb.AppendLine($"ترتيب الحي بين الأحياء: {i.Rank}");
        sb.AppendLine($"حالة متكررة: {(i.IsRepeat ? "نعم" : "لا")}");
        sb.AppendLine($"خطر صحي عاجل: {(i.UrgentHealth ? "نعم" : "لا")}");

        if (i.CycleMean is null)
        {
            sb.AppendLine("التنبؤ: غير متوفر");
        }
        else
        {
            sb.AppendLine($"دورة المياه المتوقعة بالأيام: {Fmt(i.CycleMean.Value)}");
            if (i.ProbArrival2d is not null)
                sb.AppendLine($"احتمال وصول الماء خلال يومين بالمئة: {Math.Round(i.ProbArrival2d.Value * 100)}");
            if (i.AbnormalDelay is not null)
                sb.AppendLine($"تأخر غير معتاد: {(i.AbnormalDelay.Value ? "نعم" : "لا")}");
            if (IsPriorOnly(i.PredictionBasis))
                sb.AppendLine("أساس التنبؤ: الدورة المسبقة للحي فقط، دون ملاحظات فعلية");
        }
        return sb.ToString();
    }

    private static string IssueLabel(IssueType t) =>
        t == IssueType.Contaminated ? "تلوث المياه" : "انقطاع المياه";

    private static bool IsPriorOnly(string? basis) =>
        basis?.Contains("prior only", StringComparison.OrdinalIgnoreCase) == true;

    private static string Fmt(double d) => d.ToString("0.#", CultureInfo.InvariantCulture);

    // ---------------------------------------------------------------- الرد
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

    private static string Clean(string s) =>
        s.Replace("*", "").Replace("#", "").Replace("`", "").Trim();

    /// <summary>كل رقم في نص النموذج يجب أن يكون موجوداً في المعطيات.</summary>
    private static bool NumbersAreGrounded(string output, string prompt)
    {
        var allowed = NumberRx.Matches(Normalize(prompt))
            .Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture))
            .ToList();

        foreach (Match m in NumberRx.Matches(Normalize(output)))
        {
            var v = double.Parse(m.Value, CultureInfo.InvariantCulture);
            if (!allowed.Any(a => Math.Abs(a - v) < 1e-9)) return false;
        }
        return true;
    }

    private static string Normalize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
            sb.Append(ch is >= '\u0660' and <= '\u0669' ? (char)('0' + (ch - '\u0660')) :
                      ch == '\u066B' ? '.' : ch);
        return sb.ToString();
    }

    // ---------------------------------------------------------------- القالب الاحتياطي
    private static ExplanationResult Fallback(ExplanationInput i, string error)
    {
        var s1 = $"حالة {IssueLabel(i.IssueType)} في حي {i.NeighborhoodNameAr}، عدد الأجهزة المبلِّغة {i.DistinctDevices}";
        if (i.MaxDurationDays is > 0) s1 += $"، وأطول مدة مبلَّغ عنها {i.MaxDurationDays} يوم";
        if (i.IsRepeat) s1 += "، وهي حالة متكررة";
        if (i.UrgentHealth) s1 += "، وفيها خطر صحي عاجل";
        s1 += ".";

        string s2;
        if (i.CycleMean is null)
        {
            s2 = "لا يتوفر تنبؤ لهذا الحي حالياً.";
        }
        else
        {
            s2 = $"يقدَّر أن دورة المياه في الحي نحو {Fmt(i.CycleMean.Value)} يوم";
            if (i.ProbArrival2d is not null)
                s2 += $"، واحتمال وصول الماء خلال يومين {Math.Round(i.ProbArrival2d.Value * 100)}%";
            if (i.AbnormalDelay == true) s2 += "، مع تأخر غير معتاد";
            if (IsPriorOnly(i.PredictionBasis)) s2 += "، وهو تقدير تقريبي مبني على الدورة المسبقة للحي";
            s2 += ".";
        }

        return new ExplanationResult($"{s1} {s2}", false, error);
    }
}