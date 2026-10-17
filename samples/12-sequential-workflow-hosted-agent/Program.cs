// Proposito: exponer un workflow real escritor -> revisor como Hosted Agent.
// A. Resolver la configuracion y crear el cliente del proyecto Foundry.
// B. Crear dos agentes especializados y conectarlos con el workflow secuencial de MAF.
// C. Alojar el workflow como un unico agente accesible mediante Responses.

using Azure.AI.AgentServer.Core;
using Azure.AI.Projects;
using Azure.Identity;
using LatinoNet.HostedBasics;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.Configuration;

// A. Ambos agentes usan el mismo deployment y la identidad de DefaultAzureCredential.
var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();

var projectEndpoint = new Uri(
    configuration["FOUNDRY_PROJECT_ENDPOINT"]
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is not set."));
var deployment = configuration["AZURE_AI_MODEL_DEPLOYMENT_NAME"]
    ?? throw new InvalidOperationException("AZURE_AI_MODEL_DEPLOYMENT_NAME is not set.");

var project = new AIProjectClient(projectEndpoint, new DefaultAzureCredential());
// B. El revisor recibe el borrador del escritor; no son dos llamadas independientes.
AIAgent writer = project.AsAIAgent(
    model: deployment,
    instructions: """
        Escribe una propuesta de agenda para la charla solicitada.
        Usa tres bloques, respeta la duracion indicada y responde en espanol.
        """,
    name: "LatinoNetAgendaWriter");
AIAgent reviewer = project.AsAIAgent(
    model: deployment,
    instructions: """
        Revisa la propuesta anterior y devuelve una agenda final breve en espanol.
        Verifica que los tiempos sumen la duracion solicitada.
        No prometas costos cero ni permisos automaticos.
        """,
    name: "LatinoNetAgendaReviewer");

AIAgent workflow = HostedAgendaWorkflow.Create(writer, reviewer);

// C. Foundry.Hosting adapta el workflow completo al contrato Responses.
var builder = AgentHost.CreateBuilder(args);
builder.Services.AddFoundryResponses(workflow);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
app.Run();
