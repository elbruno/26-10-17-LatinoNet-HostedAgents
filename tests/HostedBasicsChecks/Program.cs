// Proposito: comprobar samples 11, 12 y 24 sin inferencia ni ingestion en Azure.
// A. Ejecutar function calling y el workflow real de MAF con ScriptedChatClient.
// B. Capturar payloads del exporter real y comprobar sanitizacion e instrumentacion HTTP loopback.
// C. Arrancar el host con un puerto libre y verificar un solo exporter integrado; restaurar el entorno.

using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.IO.Compression;
using System.Text;
using Azure.Core.Pipeline;
using Azure.AI.AgentServer.Core;
using Azure.Monitor.OpenTelemetry.Exporter;
using LatinoNet.DemoChecks;
using LatinoNet.Demos;
using LatinoNet.HostedBasics;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

Console.WriteLine("Checks offline 11, 12 y 24. Sin inferencia ni ingestion Azure.");
const string connectionString =
    "InstrumentationKey=00000000-0000-0000-0000-000000000001;IngestionEndpoint=https://offline.invalid/";
var originalConnection = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
var originalOtlp = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
var originalPort = Environment.GetEnvironmentVariable("PORT");
using (var portProbe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0))
{
    portProbe.Start();
    Environment.SetEnvironmentVariable("PORT", ((IPEndPoint)portProbe.LocalEndpoint).Port.ToString());
}
Environment.SetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING", connectionString);
Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", null);

// A. El cliente solicita una funcion y luego verifica que su resultado vuelva al modelo.
var tools = new WorkshopTools();
var calls = 0;
using var toolClient = new ScriptedChatClient((messages, options) =>
{
    if (calls++ == 0)
    {
        var function = options?.Tools?.OfType<AIFunction>().Single()
            ?? throw new InvalidOperationException("11: no se registro CalculateBudget.");
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, [
            new FunctionCallContent("hosted-budget", function.Name,
                new Dictionary<string, object?> { ["attendees"] = 12, ["pricePerPerson"] = 25m })
        ])) { FinishReason = ChatFinishReason.ToolCalls };
    }

    var result = messages.SelectMany(message => message.Contents)
        .OfType<FunctionResultContent>().Single();
    Assert(result.CallId == "hosted-budget" && JsonSerializer.SerializeToElement(result.Result).GetDecimal() == 300m,
        "11: el resultado 300 de la tool vuelve al modelo");
    return ScriptedChatClient.Text("Presupuesto ficticio: 300.");
});
var budgetAgent = new ChatClientAgent(toolClient, name: "HostedBudgetTest", tools: tools.CreateTools());
Assert((await budgetAgent.RunAsync("Calcula 12 asistentes a 25.")).Text == "Presupuesto ficticio: 300." &&
    tools.InvocationCount == 1 && calls == 2, "11: function calling completo");
calls = 0;
var budgetChunks = new List<string>();
await foreach (var update in budgetAgent.RunStreamingAsync("Calcula 12 asistentes a 25."))
{
    budgetChunks.Add(update.Text);
}
Assert(string.Concat(budgetChunks) == "Presupuesto ficticio: 300." && tools.InvocationCount == 2,
    "11: streaming tambien ejecuta y devuelve la tool");
Expect<ArgumentOutOfRangeException>(() => tools.CalculateBudget(0, 25m), "11: rechaza cero asistentes");
Expect<ArgumentOutOfRangeException>(() => tools.CalculateBudget(1001, 25m), "11: limita asistentes");
Expect<ArgumentOutOfRangeException>(() => tools.CalculateBudget(1, -1m), "11: rechaza precio negativo");
Expect<ArgumentOutOfRangeException>(() => tools.CalculateBudget(1, 10001m), "11: limita precio");
Assert(tools.CalculateBudget(1, 0.005m) == 0.01m, "11: redondeo decimal");

// A. Comprobar la transferencia del borrador demuestra secuencia, no solo orden de mensajes.
var executionOrder = new List<string>();
using var writerClient = new ScriptedChatClient((_, _) =>
{
    executionOrder.Add("writer");
    return ScriptedChatClient.Text("Borrador unico: tres bloques de 15 minutos.");
});
using var reviewerClient = new ScriptedChatClient((messages, _) =>
{
    executionOrder.Add("reviewer");
    Assert(messages.Any(message => message.Text.Contains("Borrador unico")),
        "12: el revisor recibe el borrador del escritor");
    return ScriptedChatClient.Text("Agenda revisada: 45 minutos.");
});
var workflow = HostedAgendaWorkflow.Create(
    new ChatClientAgent(writerClient, name: "HostedWriterTest"),
    new ChatClientAgent(reviewerClient, name: "HostedReviewerTest"));
var workflowResponse = await workflow.RunAsync("Agenda de 45 minutos.");
Assert(executionOrder.SequenceEqual(["writer", "reviewer"]) &&
    workflowResponse.Text.Contains("Agenda revisada: 45 minutos."),
    "12: workflow MAF real secuencial con respuesta revisada");
executionOrder.Clear();
var workflowChunks = new List<string>();
await foreach (var update in workflow.RunStreamingAsync("Agenda de 45 minutos."))
{
    workflowChunks.Add(update.Text);
}
Assert(executionOrder.SequenceEqual(["writer", "reviewer"]) &&
    string.Concat(workflowChunks).Contains("Agenda revisada: 45 minutos."),
    "12: workflow streaming respeta el orden y entrega la revision");
// La cancelacion previa no debe ejecutar ninguno de los dos agentes, tampoco en streaming.
using (var cancelled = new CancellationTokenSource())
{
    cancelled.Cancel();
    executionOrder.Clear();
    try
    {
        await workflow.RunAsync("No debe ejecutar.", cancellationToken: cancelled.Token);
        throw new InvalidOperationException("12: ignoro la cancelacion.");
    }
    catch (OperationCanceledException)
    {
        Assert(executionOrder.Count == 0, "12: cancelacion previa no invoca escritor ni revisor");
    }
    try
    {
        await foreach (var _ in workflow.RunStreamingAsync("No ejecutar.", cancellationToken: cancelled.Token)) { }
        throw new InvalidOperationException("12: streaming ignoro la cancelacion.");
    }
    catch (OperationCanceledException)
    {
        Assert(executionOrder.Count == 0, "12: streaming cancelado no invoca agentes");
    }
}

Expect<InvalidOperationException>(() => ApplicationInsightsTelemetry.RequireConnectionString(
    new ConfigurationBuilder().Build()), "24: exige connection string");
Expect<InvalidOperationException>(() => ApplicationInsightsTelemetry.RequireConnectionString(
    Configuration("   ")), "24: rechaza connection string vacia");
Assert(ApplicationInsightsTelemetry.RequireConnectionString(Configuration(connectionString)) == connectionString,
    "24: lee la configuracion sin credenciales Azure");

var activities = new List<Activity>();
using var listener = new ActivityListener
{
    ShouldListenTo = source => source.Name == ApplicationInsightsTelemetry.SourceName,
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
    ActivityStopped = activity => activities.Add(activity)
};
ActivitySource.AddActivityListener(listener);
const string secretPrompt = "PROMPT_PRIVADO_NO_EXPORTAR";
const string secretCompletion = "COMPLETION_PRIVADA_NO_EXPORTAR";
using var observedClient = new ScriptedChatClient((_, _) => ScriptedChatClient.Text(secretCompletion));
var observed = ApplicationInsightsTelemetry.Wrap(new ChatClientAgent(observedClient, name: "ObservedOfflineTest"));
Assert((await observed.RunAsync(secretPrompt)).Text == secretCompletion, "24: conserva la respuesta");
var metadata = string.Join("\n", activities.SelectMany(activity =>
    activity.TagObjects.Select(tag => $"{tag.Key}={tag.Value}")
        .Concat(activity.Events.SelectMany(evt =>
            evt.Tags.Select(tag => $"{evt.Name}:{tag.Key}={tag.Value}")))));
Assert(activities.Count > 0 &&
    !metadata.Contains(secretPrompt) && !metadata.Contains(secretCompletion),
    "24: spans MAF reales sin prompt ni completion en tags o eventos");

// B. El SDK exporta de verdad; solo reemplazamos su transporte por un capturador sin red.
using var transport = new OfflineTelemetryHandler();
using var httpClient = new HttpClient(transport);
var tracing = Sdk.CreateTracerProviderBuilder();
ApplicationInsightsTelemetry.Configure(tracing, connectionString, configureExporter: options =>
{
    options.Transport = new HttpClientTransport(httpClient);
    options.Retry.MaxRetries = 0;
});
using (var provider = tracing.Build())
{
    await observed.RunAsync(secretPrompt);
    using var source = new ActivitySource(ApplicationInsightsTelemetry.SourceName);
    using (var span = source.StartActivity("offline-exporter-check"))
    {
        span!.SetTag("gen_ai.prompt", secretPrompt);
        span.SetTag("gen_ai.completion", secretCompletion);
        span.SetTag("tool.arguments", "ARGUMENTO_PRIVADO");
        span.SetTag("authorization", "TOKEN_PRIVADO");
        span.SetTag("url.full", "https://example.invalid/?token=QUERY_PRIVADA");
        span.SetTag("exception.message", "EXCEPCION_PRIVADA");
        span.SetTag("http.response.status_code", 200);
        span.SetStatus(ActivityStatusCode.Error, "DESCRIPCION_PRIVADA");
        span.AddBaggage("secret", "BAGGAGE_PRIVADO");
    }
    using (var span = source.StartActivity("unsafe-event"))
    {
        span!.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
        {
            ["exception.message"] = "EVENTO_PRIVADO"
        }));
    }

    // HTTP loopback real permite observar spans ASP.NET Server y HttpClient Client.
    var webBuilder = WebApplication.CreateSlimBuilder();
    webBuilder.Logging.ClearProviders();
    webBuilder.WebHost.UseUrls("http://127.0.0.1:0");
    await using var web = webBuilder.Build();
    web.MapGet("/offline", () => Results.Ok("ok"));
    await web.StartAsync();
    var address = web.Services.GetRequiredService<IServer>()
        .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    using var dependencyClient = new HttpClient();
    var response = await dependencyClient.GetAsync($"{address}/offline?token=QUERY_PRIVADA");
    Assert(response.IsSuccessStatusCode, "24: solicitud HTTP loopback real sin Azure");
    await web.StopAsync();
    Assert(provider.ForceFlush(10000), "24: el exporter real completa ForceFlush offline");
}
Assert(transport.RequestCount > 0, "24: Azure Monitor exporter usa solo el transporte simulado");
var exported = string.Join("\n", transport.Bodies);
Assert(new[] { secretPrompt, secretCompletion, "ARGUMENTO_PRIVADO", "TOKEN_PRIVADO", "QUERY_PRIVADA",
    "EXCEPCION_PRIVADA", "DESCRIPCION_PRIVADA", "BAGGAGE_PRIVADO", "EVENTO_PRIVADO" }
    .All(secret => !exported.Contains(secret)),
    "24: payload Azure Monitor sin prompts, arguments, token, query, excepciones ni baggage");
Assert(exported.Contains("RequestData") && exported.Contains("RemoteDependencyData") &&
    exported.Contains("hosted-agent request") && exported.Contains("hosted-agent dependency"),
    "24: ASP.NET requests y HttpClient dependencies llegan al exporter");
Assert(exported.Contains("hosted-agent operation"), "24: el span MAF llega al exporter sanitizado");

// C. El exporter integrado se inicializa durante StartAsync, no solamente al construir el host.
try
{
    using var hostTransport = new OfflineTelemetryHandler();
    using var hostHttp = new HttpClient(hostTransport);
    var host = AgentHost.CreateBuilder([]);
    host.WebApplicationBuilder.Logging.ClearProviders();
    host.Services.PostConfigure<AzureMonitorExporterOptions>(options =>
    {
        options.ConnectionString = connectionString;
        options.Transport = new HttpClientTransport(hostHttp);
        options.Retry.MaxRetries = 0;
        options.SamplingRatio = 1;
        options.TracesPerSecond = null;
        options.DisableOfflineStorage = true;
        options.EnableStandardMetrics = false;
        options.EnablePerformanceCounters = false;
    });
    host.Services.AddOpenTelemetry().WithTracing(builder =>
        ApplicationInsightsTelemetry.Configure(builder, connectionString, useHostExporter: true));
    await using var hostApp = host.Build().App;
    await hostApp.StartAsync();
    var provider = hostApp.Services.GetRequiredService<TracerProvider>();
    using var source = new ActivitySource(ApplicationInsightsTelemetry.SourceName);
    string traceId;
    using (var span = source.StartActivity("host-exporter-check"))
    {
        traceId = span!.TraceId.ToString();
        span.SetTag("gen_ai.prompt", secretPrompt);
        span.SetTag("tool.arguments", "ARGUMENTO_PRIVADO");
        span.SetTag("url.full", "https://example.invalid/?token=QUERY_PRIVADA");
        span.SetStatus(ActivityStatusCode.Error, "EXCEPCION_PRIVADA");
    }
    Assert(provider.ForceFlush(10000), "24: exporter integrado del host completa flush offline");
    var hostEnvelopes = string.Join("\n", hostTransport.Bodies)
        .Split('\n', StringSplitOptions.RemoveEmptyEntries);
    Assert(hostEnvelopes.Count(envelope => envelope.Contains(traceId)) == 1 &&
        new[] { secretPrompt, "ARGUMENTO_PRIVADO", "QUERY_PRIVADA", "EXCEPCION_PRIVADA" }
            .All(secret => !string.Join("", hostEnvelopes).Contains(secret)),
        "24: conexion por entorno usa un unico exporter del host y sanitiza antes de exportar");
    await hostApp.StopAsync();
}
finally
{
    Environment.SetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING", originalConnection);
    Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", originalOtlp);
    Environment.SetEnvironmentVariable("PORT", originalPort);
}

Console.WriteLine("PASS: HostedBasicsChecks completos. Deployment e ingestion Azure no validados.");

static IConfiguration Configuration(string value) => new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = value
    }).Build();

static void Assert(bool condition, string name)
{
    if (!condition) { throw new InvalidOperationException($"FAIL: {name}"); }
    Console.WriteLine($"PASS: {name}");
}

static void Expect<TException>(Action action, string name) where TException : Exception
{
    try { action(); }
    catch (TException)
    {
        Console.WriteLine($"PASS: {name}");
        return;
    }
    throw new InvalidOperationException($"FAIL: {name}");
}

// Captura el cuerpo, incluida compresion gzip, y bloquea cualquier destino distinto del ficticio.
sealed class OfflineTelemetryHandler : HttpMessageHandler
{
    private int _requestCount;
    public int RequestCount => Volatile.Read(ref _requestCount);
    public List<string> Bodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.RequestUri?.Host != "offline.invalid")
        {
            throw new InvalidOperationException("Se bloqueo un destino telemetry no simulado.");
        }
        Interlocked.Increment(ref _requestCount);
        if (request.Content is not null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            if (bytes.Length >= 2 && bytes[0] == 0x1f && bytes[1] == 0x8b)
            {
                using var gzip = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress);
                using var reader = new StreamReader(gzip);
                Bodies.Add(await reader.ReadToEndAsync(cancellationToken));
            }
            else { Bodies.Add(Encoding.UTF8.GetString(bytes)); }
        }
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"itemsReceived":1,"itemsAccepted":1,"errors":[]}""")
        };
    }
}
