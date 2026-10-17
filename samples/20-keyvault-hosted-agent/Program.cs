// 20: alojar una tool que usa una credencial externa sin entregarsela al modelo.
// A. Configurar identidad, vault y URL fija del catalogo de confianza.
// B. Registrar solo el contrato de presupuesto, no una tool de lectura de secretos.
// C. Publicar el agente mediante Responses.
using Azure.AI.AgentServer.Core;
using Azure.AI.Projects;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using LatinoNet.Hosted;
using LatinoNet.KeyVault;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;

// A. El secreto se obtiene al ejecutar la tool; nunca se incorpora a las instrucciones.
var configuration = new ConfigurationBuilder().AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables().Build();
var credential = new DefaultAzureCredential();
using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
    { Timeout = TimeSpan.FromSeconds(20) };
var catalog = new WorkshopCatalogTool(
    new SecretClient(HostedSettings.HttpsUri(configuration, "KEY_VAULT_URI"), credential),
    HostedSettings.Required(configuration, "WORKSHOP_API_SECRET_NAME"),
    HostedSettings.HttpsUri(configuration, "WORKSHOP_API_ENDPOINT"), http);
// B. El modelo puede pedir asistentes, pero no elegir el vault, secreto o destino HTTP.
var agent = new AIProjectClient(HostedSettings.HttpsUri(configuration, "FOUNDRY_PROJECT_ENDPOINT"), credential)
    .AsAIAgent(model: HostedSettings.Deployment(configuration),
        name: "LatinoNetKeyVaultAgent",
        instructions: "Consulta presupuestos de taller con la tool. No realizas cobros ni reservas. Nunca pidas ni reveles secretos.",
        tools: [AIFunctionFactory.Create(catalog.GetBudgetAsync)]);
// C. La identidad del contenedor requiere acceso explicito al secreto y al modelo.
var builder = AgentHost.CreateBuilder(args);
builder.Services.AddFoundryResponses(agent);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
var app = builder.Build();
app.Run();
