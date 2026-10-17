// Proposito: alojar un agente con traces reales de Application Insights sin exportar contenido sensible.
// A. Exigir la conexion de telemetry y resolver endpoint y deployment de Foundry.
// B. Instrumentar el agente MAF y configurar sanitizacion antes de cualquier exporter.
// C. Publicar Responses y reutilizar el exporter del host cuando lo activa el entorno.

using Azure.AI.AgentServer.Core;
using Azure.AI.Projects;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
using LatinoNet.HostedBasics;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

// A. Fallar al inicio evita presentar una demo de observabilidad sin exporter configurado.
var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();

var connectionString = ApplicationInsightsTelemetry.RequireConnectionString(configuration);
var projectEndpoint = new Uri(
    configuration["FOUNDRY_PROJECT_ENDPOINT"]
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is not set."));
var deployment = configuration["AZURE_AI_MODEL_DEPLOYMENT_NAME"]
    ?? "gpt-6-luna"; ArgumentException.ThrowIfNullOrWhiteSpace(deployment, "AZURE_AI_MODEL_DEPLOYMENT_NAME");

// B. El wrapper crea spans MAF sin habilitar captura de prompts ni completions.
AIAgent agent = ApplicationInsightsTelemetry.Wrap(
    new AIProjectClient(projectEndpoint, new DefaultAzureCredential())
        .AsAIAgent(
            model: deployment,
            instructions: "Responde en espanol de forma breve y no solicites datos personales.",
            name: "LatinoNetObservedAgent"));

var builder = AgentHost.CreateBuilder(args);
builder.WebApplicationBuilder.Configuration.AddConfiguration(configuration);
// B. El host habilita un puente de logs por defecto: no exportamos sus cuerpos.
builder.WebApplicationBuilder.Logging.AddFilter<OpenTelemetryLoggerProvider>(null, LogLevel.None);
// PostConfigure se aplica despues de los defaults de la distribucion OTel del host.
builder.Services.PostConfigure<AzureMonitorExporterOptions>(options =>
{
    options.ConnectionString = connectionString;
    options.DisableOfflineStorage = true;
    options.EnableStandardMetrics = false;
    options.EnablePerformanceCounters = false;
    options.SamplingRatio = 1;
    options.TracesPerSecond = null;
});
// Registrar antes de Build permite sanitizar tambien los spans del exporter integrado.
builder.Services.AddOpenTelemetry().WithTracing(tracing => ApplicationInsightsTelemetry.Configure(
    tracing,
    connectionString,
    useHostExporter: !string.IsNullOrEmpty(
        Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING"))));
// C. Con conexion en el entorno el host agrega su exporter; no agregamos una segunda copia.
builder.Services.AddFoundryResponses(agent);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
app.Run();
