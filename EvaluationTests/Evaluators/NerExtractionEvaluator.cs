using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
namespace EvaluationTests.Evaluators {
    // public class NerExtractionEvaluationContext : EvaluationContext {
    //     string goldenResponse;
    //     
    //     public NerExtractionEvaluationContext(string name, IEnumerable<AIContent> contents) : base(name, contents) {
    //     }
    //     public NerExtractionEvaluationContext(string name, params AIContent[] contents) : base(name, contents) {
    //     }
    //     public NerExtractionEvaluationContext(string name, string content) : base(name, content) {
    //         goldenResponse = content;
    //     }
    //
    //     public string GetGoldenResponse() {
    //         return string.Empty;
    //     }
    // }
    
    public class NerExtractionEvaluator : IEvaluator {

        const string MetricExtractionCorrectness = "NER Extraction Correctness";
        
        const string prompt = 
            """
            You are QA engineer. Evaluate accuracy and correctness of extracted entities based on original user input.
            
            Actual: {actual_json}
            User input: {user_prompt}
            
            Evaluate the accuracy from 1 to 5.
            1 - nothing or small amount of entities were extracted.
            5 - everything extracted correctly (including synonyms).
            
            Return json result in the following schema:
            {
                "accuracy": 2,
                "reason" : "here put explanation of accuracy and what's wrong in the extraction if any issues found"
            }
            """;
        
        public async ValueTask<EvaluationResult> EvaluateAsync(IEnumerable<ChatMessage> messages, ChatResponse modelResponse, ChatConfiguration? chatConfiguration = null, IEnumerable<EvaluationContext>? additionalContext = null, CancellationToken cancellationToken = new CancellationToken()) {
            if(chatConfiguration?.ChatClient == null) {
                var metric = new NumericMetric(name: MetricExtractionCorrectness, value: 0, reason: "ChatConfiguration.ChatClient is missing (no LLM judge)");
                metric.AddDiagnostics(EvaluationDiagnostic.Error("Cannot perform prompt analysis without judge client"));
                return new EvaluationResult(metric);
            }

            // if(additionalContext is null || !additionalContext.Any(x => x is NerExtractionEvaluationContext)) {
            //     var metric = new NumericMetric(name: MetricExtractionCorrectness, value: 0, reason: "NerExtractionEvaluationContext is missing in additional context");
            //     metric.AddDiagnostics(EvaluationDiagnostic.Error("Cannot perform analysis without expected response"));
            //     return new EvaluationResult(metric);
            // }
            
            // var extractionEvaluationContext = additionalContext.SingleOrDefault(x => x is NerExtractionEvaluationContext) as NerExtractionEvaluationContext;
            var userPrompt = messages.Last();
            
            ChatResponse judgeResponse;
            try {
                judgeResponse = await chatConfiguration.ChatClient.GetResponseAsync(
                    new[] {
                        // new ChatMessage(ChatRole.System, prompt),
                        new ChatMessage(ChatRole.User, GetPrompt(string.Empty, modelResponse.Text, userPrompt.Text))
                    }, cancellationToken: cancellationToken);
            }
            catch(Exception ex) {
                var metric = new NumericMetric(name: MetricExtractionCorrectness, value: 0, reason: "Failed to call LLM judge for prompt analysis");
                metric.AddDiagnostics(EvaluationDiagnostic.Error($"Judge call failed: {ex.GetType().Name}: {ex.Message}"));
                return new EvaluationResult(metric);
            }

            if(string.IsNullOrWhiteSpace(judgeResponse.Text)) {
                var metric = new NumericMetric(name: MetricExtractionCorrectness, value: 0, reason: "LLM judge returned empty response");
                metric.AddDiagnostics(EvaluationDiagnostic.Error("Empty judge response"));
                return new EvaluationResult(metric);
            }

            return CreateResultFromResponse(judgeResponse);
        }
        
        static EvaluationResult CreateResultFromResponse(ChatResponse response) {
            var result = JsonSerializer.Deserialize<NerExtractionEvaluationResult>(response.Text);

            var metric = new NumericMetric(MetricExtractionCorrectness, value: result.Accuracy, reason: result.Reason);

            return new EvaluationResult(metric);
        }

        static string GetPrompt(string expected, string actual, string userInput) {
            return prompt
                // .Replace("{expected_json}", expected)
                .Replace("{actual_json}", actual).Replace("{user_prompt}", userInput);
        }
        
        public IReadOnlyCollection<string> EvaluationMetricNames {
            get;
        }
    }

    public class NerExtractionEvaluationResult {
        [JsonPropertyName("accuracy")]
        public int Accuracy { get; set; }
        
        [JsonPropertyName("reason")]
        public string Reason { get; set; }
    }
}
