using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
namespace EvaluationTests.Evaluators {
    public class AnotherEvaluator : IEvaluator {

        const string MetricCompare = "Compare";
        
        const string prompt = 
            """
            You are QA engineer. Compare 2 JSON outputs from 2 different NER extractors and define which one is better, based on original user input.
            
            First: {first_json}
            Second: {second_json}
            User input: {user_prompt}
            
            Return json result in the following schema:
            {
                "winner": put number of json which one is better 1 or 2,
                "reason" : "here put explanation of accuracy and what's wrong in the extraction if any issues found"
            }
            """;
        
        public async ValueTask<EvaluationResult> EvaluateAsync(IEnumerable<ChatMessage> messages, ChatResponse modelResponse, ChatConfiguration? chatConfiguration = null, IEnumerable<EvaluationContext>? additionalContext = null, CancellationToken cancellationToken = new CancellationToken()) {
            if(chatConfiguration?.ChatClient == null) {
                var metric = new NumericMetric(name: MetricCompare, value: 0, reason: "ChatConfiguration.ChatClient is missing (no LLM judge)");
                metric.AddDiagnostics(EvaluationDiagnostic.Error("Cannot perform prompt analysis without judge client"));
                return new EvaluationResult(metric);
            }
            
            var userPrompt = messages.Last();
            var first = modelResponse.Messages.First();
            var second = modelResponse.Messages.Last();
            
            ChatResponse judgeResponse;
            try {
                judgeResponse = await chatConfiguration.ChatClient.GetResponseAsync(
                    new[] {
                        // new ChatMessage(ChatRole.System, prompt),
                        new ChatMessage(ChatRole.User, GetPrompt(first.Text, second.Text, userPrompt.Text))
                    }, cancellationToken: cancellationToken);
            }
            catch(Exception ex) {
                var metric = new NumericMetric(name: MetricCompare, value: 0, reason: "Failed to call LLM judge for prompt analysis");
                metric.AddDiagnostics(EvaluationDiagnostic.Error($"Judge call failed: {ex.GetType().Name}: {ex.Message}"));
                return new EvaluationResult(metric);
            }

            if(string.IsNullOrWhiteSpace(judgeResponse.Text)) {
                var metric = new NumericMetric(name: MetricCompare, value: 0, reason: "LLM judge returned empty response");
                metric.AddDiagnostics(EvaluationDiagnostic.Error("Empty judge response"));
                return new EvaluationResult(metric);
            }

            return CreateResultFromResponse(judgeResponse);
        }
        
        static EvaluationResult CreateResultFromResponse(ChatResponse response) {
            var result = JsonSerializer.Deserialize<AnotherEvaluationResult>(response.Text);

            var metric = new NumericMetric(MetricCompare, value: result.Winner, reason: result.Reason);

            return new EvaluationResult(metric);
        }

        static string GetPrompt(string first, string second, string userInput) {
            return prompt
                .Replace("{first_json}", first)
                .Replace("{second_json}", second)
                .Replace("{user_prompt}", userInput);
        }
        
        public IReadOnlyCollection<string> EvaluationMetricNames {
            get;
        }
    }
    
    public class AnotherEvaluationResult {
        [JsonPropertyName("winner")]
        public int Winner { get; set; }
        
        [JsonPropertyName("reason")]
        public string Reason { get; set; }
    }
}
