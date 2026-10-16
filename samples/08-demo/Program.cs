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

var project = new AIProjectClient(new Uri(endpoint), new DefaultAzureCredential());
AIAgent writer = project.AsAIAgent(
    model: deployment, name: "WriterAgent",
    instructions: "Propone tres bloques para una charla sobre C# y Hosted Agents. Responde en espanol.");
AIAgent reviewer = project.AsAIAgent(
    model: deployment, name: "ReviewerAgent",
    instructions: "Revisa la propuesta anterior. Devuelve una agenda final breve, sin prometer costos cero ni permisos automaticos.");

var workflowAgent = AgendaWorkflow.Create(writer, reviewer);
Console.WriteLine(await workflowAgent.RunAsync("Prepara una agenda introductoria de 45 minutos."));
