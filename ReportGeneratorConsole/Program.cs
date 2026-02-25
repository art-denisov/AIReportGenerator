// See https://aka.ms/new-console-template for more information

using System.ClientModel;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using ReportGeneratorCore;

const string AZURE_OPENAI_DEPLOYMENT = "gpt-4.1";

Console.WriteLine("=== Report Layout Generator - Console Application ===\n");
Console.WriteLine("=== START ===\n");

var azureOpenAIEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT", EnvironmentVariableTarget.User)
                          ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
var azureOpenAIApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY", EnvironmentVariableTarget.User)
                        ?? throw new InvalidOperationException("AZURE_OPENAI_API_KEY is not set.");

Console.WriteLine($"Using Azure OpenAI Endpoint: {azureOpenAIEndpoint}");
Console.WriteLine($"Using Deployment: {AZURE_OPENAI_DEPLOYMENT}\n");

var azureClient = new AzureOpenAIClient(new Uri(azureOpenAIEndpoint), new ApiKeyCredential(azureOpenAIApiKey)); 
var chatClient = azureClient.GetChatClient(AZURE_OPENAI_DEPLOYMENT).AsIChatClient();

var gen = new ReportGenerator(chatClient);

using var cts = new CancellationTokenSource();

var result = await gen.GenerateAsync(
    "Sales report. Columns: Date, Customer, Amount.",
    onEvent: evt => Console.WriteLine(evt.ToString()),
    ct: cts.Token);

Console.WriteLine($"Valid: {result.Validation.IsValid}, Title: {result.Spec.Title}");

Console.WriteLine("=== FINISH ===\n");