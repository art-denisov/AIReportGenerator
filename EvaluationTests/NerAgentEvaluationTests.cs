using System.ClientModel;
using System.Text.Json;
using Azure.AI.OpenAI;
using EvaluationTests.Evaluators;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using ReportGeneratorCore;
using ReportGeneratorCore.DTOs;
using ReportGeneratorCore.Executors;
namespace EvaluationTests;

public class NerAgentEvaluationTests {

    const string azureOpenAIEndpoint = "https://public-api.devexpress.com/demo-openai";
    const string azureOpenAIApiKey = "DEMO";

    AzureOpenAIClient azureClient = new AzureOpenAIClient(new Uri(azureOpenAIEndpoint), new ApiKeyCredential(azureOpenAIApiKey));

    // [TestCase("I need a standard invoice for a client. At the top — the company logo, a large “Invoice” title, invoice number, issue date, and due date. On the left — client information (bill to), on the right — shipping address if different. The main section should contain a table of line items: description of product or service, quantity, unit price, and line total. At the bottom, show subtotal, tax, shipping if applicable, and prominently highlight the total amount due. At the end, add payment terms and a short message such as “Thank you for your business.” Style — professional and clean.", "gpt-4.1")]
    [TestCase("I need an Employee Directory report. At the top — logo and “Employee Directory” title. Group employees by department. For each employee show photo, name, position, phone, and email. At the end of each department show the number of employees, and at the bottom of the report show the total count. Style — modern and clean, with alternating row shading for readability.", "gpt-4.1")]
    public async Task NerAgentExtractionCorrectness(string userPrompt, string model) {
        var chatClient = azureClient.GetChatClient(model).AsIChatClient();
        var nerExecutor = new SpecExecutor(chatClient);
        var stupidExecutor = new StupidExecutor(chatClient);
        // var validate = new ValidateSpecExecutor();
        var workflow = new WorkflowBuilder(nerExecutor)
            // .AddEdge(nerExecutor, validate)
            // .WithOutputFrom(validate)
            .WithOutputFrom(nerExecutor)
            .Build();

        var workflow2 = new WorkflowBuilder(stupidExecutor)
          .WithOutputFrom(stupidExecutor)
          .Build();
        
        var result = await InProcessExecution.RunAsync(workflow, userPrompt);
        var result2 = await InProcessExecution.RunAsync(workflow2, userPrompt);
        
        WorkflowEvent workflowEvent = result.NewEvents.Single(x => x is WorkflowOutputEvent o && o.Is<ReportSpec>());
        WorkflowEvent workflowEvent2 = result2.NewEvents.Single(x => x is WorkflowOutputEvent o && o.Is<string>());
        
        var data = (workflowEvent as WorkflowOutputEvent).Data as ReportSpec;
        var data2 = (workflowEvent2 as WorkflowOutputEvent).Data as string;

        // var a = 1;

        // var generator = new ReportGenerator(chatClient);
        //
        // var result = await generator.GenerateAsync(userPrompt);
        //
        var evaluator = new NerExtractionEvaluator();
        var evaluator2 = new AnotherEvaluator();

        var evaluationResult = await evaluator.EvaluateAsync(new[] { new ChatMessage(ChatRole.System, systemPrompt), new ChatMessage(ChatRole.User, userPrompt) }, new ChatResponse(new ChatMessage(ChatRole.Assistant, JsonSerializer.Serialize(data))), new ChatConfiguration(chatClient));
        var evaluationResult3 = await evaluator.EvaluateAsync(new[] { new ChatMessage(ChatRole.System, systemPrompt2), new ChatMessage(ChatRole.User, userPrompt) }, new ChatResponse(new ChatMessage(ChatRole.Assistant, data2)), new ChatConfiguration(chatClient));
        var evaluationResult2 = await evaluator2.EvaluateAsync(new[] { new ChatMessage(ChatRole.User, userPrompt) }, new ChatResponse(new List<ChatMessage> { new ChatMessage(ChatRole.Assistant, JsonSerializer.Serialize(data)), new ChatMessage(ChatRole.Assistant, data2) }), new ChatConfiguration(chatClient));
    }
    
    const string systemPrompt = """
                          You are a strict semantic NER extractor.
                          
                          Your task is to extract a structured report specification from the user text and return ONLY valid JSON.
                          No markdown. No explanations. No comments. No extra text.
                          
                          CRITICAL RULES:
                          - DO NOT INVENT ANYTHING.
                          - Extract only what is explicitly stated, or what can be safely and unambiguously inferred.
                          - If something is not mentioned — use null (for scalar fields) or [] (for arrays).
                          - Do NOT force values into predefined enums. All string fields are open vocabulary.
                          - Preserve the user’s original language and wording for titles and column names.
                          - Do NOT create technical/internal data source field names (e.g., do not convert “unit price” into “UnitPrice”).
                          - Be conservative with DataType inference; if unsure, return null.
                          
                          INPUT:
                          A single user request describing a report.
                          
                          OUTPUT:
                          Return exactly one JSON object with this schema:
                          
                          {
                            "ReportType": string|null,
                            "Title": string|null,
                            "Orientation": string|null,
                            "PageSize": string|null,
                            "Theme": string|null,
                            "GroupBy": string[],
                            "SortBy": string[],
                            "SortingType": string|null,
                            "Columns": [
                              {
                                "Field": string,
                                "Title": string|null,
                                "DataType": string|null
                              }
                            ],
                            "OtherValuableTokens": string[]
                          }
                          
                          EXTRACTION RULES:
                          
                          1) ReportType
                          Extract the semantic type/name of the report as stated or clearly implied.
                          Examples: "Invoice", "Purchase Order", "Statement of Account", "Sales Report", "Employee Directory", "Packing Slip".
                          If not clear → null.
                          
                          2) Title
                          Extract only if the user explicitly specifies the header/title text (e.g., “Invoice”, “Employee Directory”, “Balance”).
                          If not explicit → null.
                          
                          3) Orientation
                          Extract only if explicitly stated (e.g., "Landscape", "Portrait", "Horizontal", "Vertical").
                          Otherwise → null.
                          
                          4) PageSize
                          Extract only if explicitly stated (e.g., "A4", "Letter", "receipt roll 80mm", "custom 210x297mm").
                          Return the user wording as-is (or a minimal normalized phrase while preserving meaning, e.g., "Receipt roll 80 mm").
                          If not stated → null.
                          
                          5) Theme
                          Extract explicit style requirements only if mentioned (e.g., "professional", "modern", "corporate", "strict", "black-and-white", "print-friendly").
                          Preserve wording.
                          If not stated → null.
                          
                          6) GroupBy
                          Fill only if the user explicitly requests grouping (e.g., “group by department”, “customers with orders under each customer”, “category → subcategory”).
                          Preserve hierarchy order: top level → lower level.
                          If not stated → [].
                          
                          7) SortBy
                          Fill only if the user explicitly requests sorting and names the sort field(s) (e.g., “sort by date”, “order by amount”).
                          Preserve wording as written.
                          If not stated → [].
                          
                          8) SortingType
                          Fill only if direction is explicitly stated or unambiguous:
                          Examples: "ascending", "descending", "newest first", "oldest first", "high to low", "low to high".
                          If not stated → null.
                          
                          9) Columns
                          Add entries only when the user explicitly lists columns/fields to display (e.g., “columns: date, customer, amount”).
                          For each column:
                          - Field: required. Use the exact label from the text (e.g., "цена за единицу", "сумма по строке", "VIN", "телефон", "email").
                          - Title: only if the user explicitly provides a display caption that differs from Field; otherwise null.
                          - DataType: infer conservatively when obvious:
                            - "Date" / "DateTime" for dates/timestamps
                            - "Currency" for amounts, totals, price, revenue, cost
                            - "Number" for quantity, hours, counts (if clearly numeric)
                            - "Percent" for percentage values
                            - "String" for text fields (names, addresses, document numbers, VIN, codes, categories)
                            If not obvious → null.
                          
                          Do NOT add columns that are not explicitly requested.
                          
                          10) OtherValuableTokens
                          Capture important requirements that don’t fit into fields above, only if explicitly present:
                          Examples: "logo", "billing/shipping blocks", "signature", "customer signature", "electronic signature", "terms and conditions",
                          "thank you message", "KPI cards", "chart", "graph", "dashboard layout", "alternating rows", "highlight low stock",
                          "aging buckets 0–30/30–60", "carry-over totals", "embed PDF", "append external PDF", "print black-and-white".
                          Use short phrases. Do not add anything not present.
                          
                          OUTPUT REQUIREMENTS:
                          - Return ONLY JSON.
                          - Use null for missing scalar fields.
                          - Use [] for missing lists.
                          - Do not include any keys beyond the schema.
                          - Do not include trailing commas.
                          - Ensure valid JSON string escaping.
                          
                          Now extract the report spec from this user text:
                          """;

    const string systemPrompt2 =
      """
      You are a specialized NER (Named Entity Recognition) agent in a multi-agent report generation workflow.

      You are Agent #1 in the pipeline.

      Your responsibility:
      Extract structured report configuration entities from an unstructured user prompt.

      Downstream agents depend strictly on your structured JSON output.

      ---

      # Critical Rules

      - DO NOT output fields with null values.
      - DO NOT output empty arrays.
      - Only include properties that were confidently extracted.
      - Always include the original input data in your response.
      - Output STRICT JSON only (no markdown, no commentary).
      - If you cannot extract any entities, return only the input field.

      ---

      # Output Format

      ```json
      {
        "input": {
          "userPrompt": "original user prompt",
          "dataSchema": "original data schema"
        },
        "reportType": "extracted type",
        "dataFields": ["Field1", "Field2"],
        ...other extracted entities
      }
      ```

      If nothing can be extracted:
      ```json
      {
        "input": {
          "userPrompt": "original user prompt",
          "dataSchema": "original data schema"
        }
      }
      ```

      ---

      # Entities To Extract

      ## ReportType (examples)

      - payroll
      - invoice
      - sales
      - inventory
      - finance
      - marketing
      - HR
      - operations

      ---

      ## DataFields (examples)

      - EmployeeName
      - Salary
      - Revenue
      - Date
      - Department
      - CustomerName
      - Product
      - Quantity
      - Profit
      - Region

      ---

      ## Aggregations (canonical only)

      - Sum
      - Count
      - Avg
      - Min
      - Max
      - Median

      Normalization rules:
      - "total" -> Sum
      - "average" / "mean" -> Avg
      - "number of" -> Count

      ---

      ## Groupings (examples)

      - Department
      - Month
      - Year
      - Region
      - Customer
      - ProductCategory

      ---

      ## Filters

      Return normalized, human-readable conditions.

      Examples:

      - Date between 2025-01-01 and 2025-12-31
      - Region = EMEA
      - Amount > 1000

      ---

      ## StylePreference

      Allowed examples:

      - Professional
      - Executive
      - Minimal
      - Detailed
      - Visual
      - Corporate

      ---

      ## PageSettings

      - pageSize: A4, Letter
      - orientation: Portrait, Landscape
      - margins: Narrow, Normal, Wide (or numeric if explicitly provided)

      ---

      # Inference Rules

      - You MAY infer implied entities.
      - Include inferred entities ONLY if confidence >= 80%.
      - Never output guesses.
      - Never output multiple alternatives.
      - Never hallucinate unsupported fields.

      Example:

      Input:
      "total salary by department"

      Extract:
      - reportType: payroll
      - dataFields: Salary
      - aggregations: Sum
      - groupings: Department

      ---

      # Validation Rule

      Return:

      {
        "error": true,
        "reasoning": "Explanation"
      }

      If:

      - The request is unrelated to report generation, OR
      - You cannot confidently extract at least one meaningful report entity.

      """;
}
