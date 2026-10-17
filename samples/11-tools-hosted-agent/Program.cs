// Proposito: alojar un agente que calcula presupuestos con una funcion C# real.
// A. Leer endpoint y deployment sin incluir credenciales en el codigo.
// B. Registrar WorkshopTools para que MAF ejecute CalculateBudget y devuelva su resultado al modelo.
// C. Publicar el agente mediante el protocolo Responses de Foundry.Hosting.

using Azure.AI.AgentServer.Core;
using Azure.AI.Projects;
using Azure.Identity;
using LatinoNet.Demos;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.Configuration;

// A. User-secrets facilita la demo local; las variables de entorno tienen prioridad.
var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();

var projectEndpoint = new Uri(
    configuration["FOUNDRY_PROJECT_ENDPOINT"]
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is not set."));
var deployment = configuration["AZURE_AI_MODEL_DEPLOYMENT_NAME"]
    ?? throw new InvalidOperationException("AZURE_AI_MODEL_DEPLOYMENT_NAME is not set.");

// B. La tool vive en el proceso del host, no en un servicio remoto ni en el prompt.
var tools = new WorkshopTools();
AIAgent agent = new AIProjectClient(projectEndpoint, new DefaultAzureCredential())
    .AsAIAgent(
        model: deployment,
        instructions: """
            Responde en espanol y de forma breve. Para calcular el presupuesto de un taller,
            usa CalculateBudget con los asistentes y el precio por persona indicados.
            Si falta un dato, solicitalo. El presupuesto es ficticio, no cobra ni reserva.
            """,
        name: "LatinoNetBudgetAgent",
        tools: tools.CreateTools());

// C. El mismo agente con tools atiende solicitudes Responses, incluidas las de streaming.
var builder = AgentHost.CreateBuilder(args);
builder.Services.AddFoundryResponses(agent);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
app.Run();
