// 10: conservar el agente de 01 y alojarlo mediante el protocolo Responses.
// A. Leer la misma configuracion de endpoint y deployment.
// B. Construir el mismo agente con una credencial compatible con el runtime.
// C. Registrar Responses y ejecutar el servidor HTTP.
using Azure.AI.AgentServer.Core;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.Configuration;

// A. Las variables de entorno prevalecen sobre User Secrets.
var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();

var projectEndpoint = new Uri(
    configuration["FOUNDRY_PROJECT_ENDPOINT"]
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is not set."));
var deployment = configuration["AZURE_AI_MODEL_DEPLOYMENT_NAME"]
    ?? "gpt-6-luna";

// B. La identidad del runtime necesita permisos, no se conceden automaticamente.
AIAgent agent = new AIProjectClient(projectEndpoint, new DefaultAzureCredential())
    .AsAIAgent(
        model: deployment,
        instructions: "You are a friendly assistant. Keep your answers brief.",
        name: "LatinoNetHelloAgent");

// C. Cambia la forma de ejecutar el agente, no sus instrucciones ni su nombre.
var builder = AgentHost.CreateBuilder(args);
builder.Services.AddFoundryResponses(agent);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
app.Run();
