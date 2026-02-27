using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using ReportGeneratorCore.DTOs;
using ReportGeneratorCore.Prompts;
namespace ReportGeneratorCore.Executors {
    internal sealed partial class StupidExecutor(IChatClient chat) : Executor("SpecAgent") {
        readonly IChatClient chat = chat ?? throw new ArgumentNullException(nameof(chat));

        const string resourceNamespace = "ReportGeneratorCore.Prompts";
        static readonly Assembly assembly = typeof(ReportGenerator).Assembly;

        [MessageHandler]
        async ValueTask<string> HandleAsync(string requirements, IWorkflowContext context, CancellationToken ct) {
            if(string.IsNullOrWhiteSpace(requirements))
                throw new ArgumentException("Requirements must be non-empty.", nameof(requirements));

            var messages = BuildPrompt(requirements, PromptFileNames.NER_Pasha);

            var options = new ChatOptions { Temperature = 0 };

            var response = await chat
                .GetResponseAsync(messages, options, ct)
                .ConfigureAwait(false);

            // var json = ExtractJsonObject(response.Text);
            //
            // var spec = ParseSpec(json);
            return response.Text;
        }

        static List<ChatMessage> BuildPrompt(string userPrompt, string systemPromptFile) {
            if(string.IsNullOrWhiteSpace(systemPromptFile))
                throw new ArgumentException("Prompt file name cannot be null or empty.", nameof(systemPromptFile));

            var resourceName = $"{resourceNamespace}.{systemPromptFile}";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if(stream == null)
                throw new FileNotFoundException($"Embedded resource not found: {resourceName}", resourceName);

            using var reader = new StreamReader(stream);
            var systemPrompt = reader.ReadToEnd();

            return [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, userPrompt)
            ];
        }

        static ReportSpec ParseSpec(string json) {
            try {
                var options = new JsonSerializerOptions {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true
                };

                return JsonSerializer.Deserialize<ReportSpec>(json, options) ?? new ReportSpec();
            }
            catch(JsonException je) {
                throw new InvalidOperationException("LLM returned invalid JSON for ReportSpec.", je);
            }
        }

        static string ExtractJsonObject(string? text) {
            if(string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException("LLM returned empty response.");

            var s = text.Trim();

            if(s.StartsWith('{'))
                return s;

            var sb = new StringBuilder();
            var depth = 0;
            var started = false;

            foreach(var ch in s) {
                if(!started) {
                    if(ch == '{') {
                        started = true;
                        depth = 1;
                        sb.Append(ch);
                    }

                    continue;
                }

                sb.Append(ch);

                switch(ch) {
                    case '{':
                        depth++;
                        break;

                    case '}': {
                        depth--;
                        if(depth == 0)
                            return sb.ToString();

                        break;
                    }
                }
            }

            throw new InvalidOperationException("Could not extract JSON object from LLM response.");
        }
    }
}
