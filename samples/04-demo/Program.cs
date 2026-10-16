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

var tools = new WorkshopTools();
AIAgent agent = new AIProjectClient(new Uri(endpoint), new DefaultAzureCredential())
    .AsAIAgent(
        model: deployment,
        instructions: "Responde en espanol. Para calcular el presupuesto usa la herramienta, no calcules de memoria.",
        name: "ToolsAgent",
        tools: tools.CreateTools());

Console.WriteLine(await agent.RunAsync("Calcula el presupuesto ficticio para 12 asistentes a 25 por persona."));
Console.WriteLine($"Invocaciones reales de la tool: {tools.InvocationCount}");
if (tools.InvocationCount == 0)
{
    throw new InvalidOperationException("El modelo no invoco la tool. No presentar esta respuesta como una prueba de tool calling.");
}
