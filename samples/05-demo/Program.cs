using System.Text.Json;
using Azure.AI.Projects;
using Azure.Identity;
using LatinoNet.Demos;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();
var endpoint = configuration["FOUNDRY_PROJECT_ENDPOINT"]
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is not set.");
var deployment = configuration["AZURE_AI_MODEL_DEPLOYMENT_NAME"]
    ?? throw new InvalidOperationException("AZURE_AI_MODEL_DEPLOYMENT_NAME is not set.");

AIAgent agent = new AIProjectClient(new Uri(endpoint), new DefaultAzureCredential())
    .AsAIAgent(model: deployment, instructions: "Extrae solo los datos presentes en la solicitud.", name: "StructuredAgent");

var response = await agent.RunAsync<TalkPlan>(
    "Titulo: De localhost a Foundry. Duracion: 45 minutos. Temas: C#, Responses, Hosted Agents.");
response.Result.Validate();
Console.WriteLine(JsonSerializer.Serialize(response.Result, new JsonSerializerOptions { WriteIndented = true }));
