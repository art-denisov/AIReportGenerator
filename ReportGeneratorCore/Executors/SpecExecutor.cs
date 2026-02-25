using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

internal sealed partial class SpecExecutor : Executor
{
    private readonly IChatClient _chat;

    public SpecExecutor(IChatClient chat)
        : base("SpecAgent")
    {
        _chat = chat ?? throw new ArgumentNullException(nameof(chat));
    }

    [MessageHandler]
    private async ValueTask<ReportSpec> HandleAsync(string requirements, IWorkflowContext context, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(requirements))
            throw new ArgumentException("Requirements must be non-empty.", nameof(requirements));

        var messages = BuildPrompt(requirements);

        var options = new ChatOptions
        {
            Temperature = 0
        };

        var response = await _chat
            .GetResponseAsync(messages, options, ct)
            .ConfigureAwait(false);

        var json = ExtractJsonObject(response.Text);

        var spec = ParseSpec(json);

        // Нормализация (защита от мусора).
        spec = Normalize(spec);

        return spec;
    }

    private static List<ChatMessage> BuildPrompt(string requirements)
    {
        // В проде рекомендую подмешивать схему/примеры и запрещать любое другое содержание.
        // Здесь — минимально, но строго.
        const string system =
            "You are a report specification generator.\n" +
            "Return STRICT JSON ONLY. No markdown, no code fences, no commentary.\n" +
            "Schema:\n" +
            "{ \"title\": string, \"columns\": string[] }\n" +
            "Rules:\n" +
            "- title: short human-readable title\n" +
            "- columns: 1..20 column captions (strings)\n";

        return new List<ChatMessage>
        {
            new(ChatRole.System, system),
            new(ChatRole.User, requirements),
        };
    }

    private static ReportSpec ParseSpec(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var title = root.TryGetProperty("title", out var t) ? t.GetString() : null;
            var cols = root.TryGetProperty("columns", out var c) && c.ValueKind == JsonValueKind.Array
                ? c.EnumerateArray().Select(x => x.GetString()).ToArray()
                : Array.Empty<string?>();

            return new ReportSpec(title ?? "Report", cols.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).ToArray());
        }
        catch (JsonException je)
        {
            // В проде полезно логировать сырой ответ LLM.
            throw new InvalidOperationException("LLM returned invalid JSON for ReportSpec.", je);
        }
    }

    private static ReportSpec Normalize(ReportSpec spec)
    {
        var title = (spec.Title ?? "").Trim();
        if (title.Length == 0) title = "Report";
        if (title.Length > 120) title = title[..120].Trim();

        var columns = (spec.Columns ?? Array.Empty<string>())
            .Select(c => (c ?? "").Trim())
            .Where(c => c.Length > 0)
            .Select(c => c.Length > 60 ? c[..60].Trim() : c)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();

        if (columns.Length == 0)
            columns = new[] { "Name", "Value" };

        return new ReportSpec(title, columns);
    }

    /// <summary>
    /// Вытаскивает первый JSON-объект из текста. Устойчиво к "Here is JSON: {...}" и т.п.
    /// </summary>
    private static string ExtractJsonObject(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("LLM returned empty response.");

        var s = text.Trim();

        // Быстрый путь: уже JSON-объект
        if (s.StartsWith("{", StringComparison.Ordinal))
            return s;

        // Медленный путь: ищем первую сбалансированную пару { ... }
        var sb = new StringBuilder();
        var depth = 0;
        var started = false;

        foreach (var ch in s)
        {
            if (!started)
            {
                if (ch == '{')
                {
                    started = true;
                    depth = 1;
                    sb.Append(ch);
                }
                continue;
            }

            sb.Append(ch);

            if (ch == '{') depth++;
            else if (ch == '}')
            {
                depth--;
                if (depth == 0)
                    return sb.ToString();
            }
        }

        throw new InvalidOperationException("Could not extract JSON object from LLM response.");
    }
}