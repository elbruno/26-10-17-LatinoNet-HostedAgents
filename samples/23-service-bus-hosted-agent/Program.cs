// 23: separar la propuesta del modelo y la aprobacion humana que envia a Service Bus.
// A. Configurar el sender y el token privado de la aplicacion que aprueba.
// B. Registrar solamente ProposeAsync como tool del modelo.
// C. Publicar revision/confirmacion autenticadas fuera del contrato de tools.
using Azure.AI.AgentServer.Core;
using Azure.AI.Projects;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using LatinoNet.Hosted;
using LatinoNet.Jobs;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;

// A. El token no es una instruccion para el modelo ni una credencial de Azure.
var configuration = new ConfigurationBuilder().AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables().Build();
var credential = new DefaultAzureCredential();
var approvalToken = HostedSettings.Required(configuration, "JOB_APPROVAL_TOKEN");
if (approvalToken.Length < 32) throw new InvalidOperationException("JOB_APPROVAL_TOKEN must contain at least 32 random characters.");
await using var bus = new ServiceBusClient(HostedSettings.Required(configuration, "SERVICE_BUS_NAMESPACE"), credential);
await using var sender = bus.CreateSender(HostedSettings.Required(configuration, "SERVICE_BUS_QUEUE"));
var jobs = new JobTools(async (id, payload, cancellationToken) =>
    await sender.SendMessageAsync(new ServiceBusMessage(payload) { MessageId = id, ContentType = "application/json" }, cancellationToken));
// B. ConfirmAsync NO forma parte de este catalogo de tools.
var agent = new AIProjectClient(HostedSettings.HttpsUri(configuration, "FOUNDRY_PROJECT_ENDPOINT"), credential)
    .AsAIAgent(model: HostedSettings.Deployment(configuration),
        name: "LatinoNetQueueAgent",
        instructions: "Solo puedes proponer trabajos ficticios. Devuelve el identificador y explica que falta confirmacion humana fuera del chat. No afirmes que se envio ni ejecuto y nunca pidas el token de aprobacion.",
        tools: [AIFunctionFactory.Create(jobs.ProposeAsync)]);
var builder = AgentHost.CreateBuilder(args);
builder.Services.AddFoundryResponses(agent);
// C. La aplicacion revisa el registro real antes de confirmar; el chat no se autoautoriza.
builder.RegisterProtocol("responses", endpoints =>
{
    endpoints.MapFoundryResponses();
    endpoints.MapGet("/jobs/{id}", async (string id, HttpContext context, CancellationToken cancellationToken) =>
    {
        if (!JobTools.IsAuthorized(context.Request.Headers.Authorization.ToString(), approvalToken))
            return Results.Unauthorized();
        if (!Guid.TryParseExact(id, "N", out _)) return Results.BadRequest(new { error = "Invalid proposal id." });
        var proposal = await jobs.GetProposalAsync(id, cancellationToken);
        return proposal is not null
            ? Results.Ok(proposal)
            : Results.NotFound(new { error = "Proposal not found, expired or already confirmed." });
    });
    endpoints.MapPost("/jobs/{id}/confirm", async (string id, HttpContext context, CancellationToken cancellationToken) =>
    {
        if (!JobTools.IsAuthorized(context.Request.Headers.Authorization.ToString(), approvalToken))
            return Results.Unauthorized();
        if (!Guid.TryParseExact(id, "N", out _)) return Results.BadRequest(new { error = "Invalid proposal id." });
        return await jobs.ConfirmAsync(id, cancellationToken)
            ? Results.Ok(new { id, status = "queued" })
            : Results.NotFound(new { error = "Proposal not found, expired or already confirmed." });
    });
});
var app = builder.Build();
await app.RunAsync();
