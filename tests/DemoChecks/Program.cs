// Checks offline de 01 a 09: ejercen MAF con respuestas controladas, no con Foundry.
// A. Construir agentes con ScriptedChatClient.
// B. Comprobar streaming, sesiones, tools, contratos y workflows reales del framework.
// C. Verificar observabilidad y cancelacion, propagando cualquier fallo.
using LatinoNet.DemoChecks;
using LatinoNet.Demos;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

Console.WriteLine("Pruebas offline con IChatClient simulado. No son respuestas de Foundry.");

using var basicClient = new ScriptedChatClient((_, _) => ScriptedChatClient.Text("Respuesta de prueba"));
var basic = new ChatClientAgent(basicClient, name: "BasicTest");
Assert((await basic.RunAsync("Hola")).Text == "Respuesta de prueba", "01: agente de consola");

var chunks = new List<string>();
await foreach (var update in basic.RunStreamingAsync("Hola streaming"))
{
    chunks.Add(update.Text);
}
Assert(string.Concat(chunks) == "Respuesta de prueba", "02: streaming conserva el texto");

var session = await basic.CreateSessionAsync();
await basic.RunAsync("Dato de prueba: charla de 45 minutos.", session);
await basic.RunAsync("Recuerda mi charla.", session);
Assert(basicClient.Requests[^1].Any(message => message.Text.Contains("Dato de prueba")),
    "03: historial presente en la misma sesion");
var isolated = await basic.CreateSessionAsync();
await basic.RunAsync("Otra sesion.", isolated);
Assert(!basicClient.Requests[^1].Any(message => message.Text.Contains("Dato de prueba")),
    "03: aislamiento del historial");

var tools = new WorkshopTools();
var toolCall = 0;
using var toolClient = new ScriptedChatClient((messages, options) =>
{
    if (toolCall++ == 0)
    {
        var function = options?.Tools?.OfType<AIFunction>().Single()
            ?? throw new InvalidOperationException("No se registro la tool.");
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, [
            new FunctionCallContent("budget-call", function.Name,
                new Dictionary<string, object?> { ["attendees"] = 12, ["pricePerPerson"] = 25m })
        ])) { FinishReason = ChatFinishReason.ToolCalls };
    }
    Assert(messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Any(),
        "04: el resultado de tool regresa al modelo");
    return ScriptedChatClient.Text("Presupuesto de prueba: 300.");
});
var toolAgent = new ChatClientAgent(toolClient, name: "ToolTest", tools: tools.CreateTools());
await toolAgent.RunAsync("Calcula el presupuesto.");
Assert(tools.InvocationCount == 1 && tools.CalculateBudget(12, 25m) == 300m, "04: tool ejecutada con resultado correcto");
Expect<ArgumentOutOfRangeException>(() => tools.CalculateBudget(0, 25m), "04: asistentes invalidos");
Expect<ArgumentOutOfRangeException>(() => tools.CalculateBudget(12, -1m), "04: precio negativo");
Expect<ArgumentOutOfRangeException>(() => tools.CalculateBudget(1001, 25m), "04: limite de asistentes");
Expect<ArgumentOutOfRangeException>(() => tools.CalculateBudget(1, 10001m), "04: limite de precio");

using var structuredClient = new ScriptedChatClient((_, _) =>
    ScriptedChatClient.Text("""{"Title":"De localhost a Foundry","Minutes":45,"Topics":["C#","Responses"]}"""));
var structured = new ChatClientAgent(structuredClient, name: "StructuredTest");
var result = await structured.RunAsync<TalkPlan>("Extrae el plan.");
result.Result.Validate();
Assert(result.Result.Minutes == 45 && result.Result.Topics.Length == 2 &&
    structuredClient.LastOptions?.ResponseFormat is ChatResponseFormatJson,
    "05: esquema JSON y resultado tipado");
Expect<InvalidOperationException>(() => new TalkPlan("", 0, []).Validate(), "05: validacion semantica");
using var invalidJsonClient = new ScriptedChatClient((_, _) => ScriptedChatClient.Text("not-json"));
try
{
    var invalidResponse = await new ChatClientAgent(invalidJsonClient).RunAsync<TalkPlan>("Extrae el plan.");
    invalidResponse.Result.Validate();
    throw new InvalidOperationException("Se acepto JSON invalido.");
}
catch (System.Text.Json.JsonException)
{
    Console.WriteLine("PASS: 05: JSON invalido no se convierte en exito");
}

var logs = new List<string>();
var timed = TimingMiddleware.Wrap(basic, logs.Add);
Assert((await timed.RunAsync("Contenido que no debe ir al log")).Text == "Respuesta de prueba" &&
    logs.Count == 2 && !string.Join("", logs).Contains("Contenido que"),
    "06: middleware conserva la respuesta sin registrar prompts");
logs.Clear();
await foreach (var update in timed.RunStreamingAsync("Contenido streaming")) { }
Assert(logs.Count == 2, "06: middleware tambien envuelve streaming");
using var failureClient = new ScriptedChatClient((_, _) => throw new InvalidOperationException("Fallo simulado"));
logs.Clear();
try
{
    await TimingMiddleware.Wrap(new ChatClientAgent(failureClient), logs.Add).RunAsync("Falla.");
    throw new InvalidOperationException("Se oculto el error.");
}
catch (InvalidOperationException error) when (error.Message == "Fallo simulado")
{
    Assert(logs.Count == 2, "06: middleware propaga error y completa medicion");
}

using var specialistClient = new ScriptedChatClient((_, _) => ScriptedChatClient.Text("Revision de prueba"));
var specialist = new ChatClientAgent(specialistClient, name: "AgendaReviewer", description: "Revisa una agenda.");
var specialistTool = specialist.AsAIFunction();
var toolArguments = specialistTool.JsonSchema.GetProperty("properties")
    .EnumerateObject().ToDictionary(property => property.Name, _ => (object?)"Revisa esta agenda de prueba.");
var delegated = await specialistTool.InvokeAsync(new AIFunctionArguments(toolArguments));
Assert(specialistClient.Requests.Count == 1 && delegated is not null, "07: agente invocado como AIFunction");
var coordinatorCalls = 0;
using var coordinatorClient = new ScriptedChatClient((messages, _) =>
{
    if (coordinatorCalls++ == 0)
    {
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, [
            new FunctionCallContent("review-call", specialistTool.Name, toolArguments)
        ])) { FinishReason = ChatFinishReason.ToolCalls };
    }
    Assert(messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Any(),
        "07: la revision vuelve al coordinador como resultado de tool");
    return ScriptedChatClient.Text("Agenda coordinada de prueba");
});
var coordinator = new ChatClientAgent(coordinatorClient, name: "CoordinatorTest", tools: [specialistTool]);
Assert((await coordinator.RunAsync("Pide una revision.")).Text == "Agenda coordinada de prueba" &&
    specialistClient.Requests.Count == 2, "07: delegacion completa desde el coordinador");

using var writerClient = new ScriptedChatClient((_, _) => ScriptedChatClient.Text("Borrador de prueba"));
using var reviewerClient = new ScriptedChatClient((messages, _) =>
{
    Assert(messages.Any(message => message.Text.Contains("Borrador de prueba")),
        "08: el revisor recibe la salida del escritor");
    return ScriptedChatClient.Text("Agenda final de prueba");
});
var workflow = AgendaWorkflow.Create(
    new ChatClientAgent(writerClient, name: "WriterTest"),
    new ChatClientAgent(reviewerClient, name: "ReviewerTest"));
var workflowResponse = await workflow.RunAsync("Crea una agenda.");
Assert(workflowResponse.Text.Contains("Agenda final de prueba") &&
    writerClient.Requests.Count == 1 && reviewerClient.Requests.Count == 1,
    "08: workflow secuencial real de MAF");

var traces = new List<string>();
using (DemoTelemetry.Listen(traces.Add))
{
    var observed = DemoTelemetry.Wrap(basic);
    await observed.RunAsync("Prompt que no debe registrarse");
}
Assert(traces.Count > 0 && !string.Join("", traces).Contains("Prompt que"),
    "09: span real del agente sin contenido sensible");

using var cancelled = new CancellationTokenSource();
cancelled.Cancel();
try
{
    await basic.RunAsync("Cancelado.", cancellationToken: cancelled.Token);
    throw new InvalidOperationException("Se ignoro la cancelacion.");
}
catch (OperationCanceledException)
{
    Console.WriteLine("PASS: cancelacion propagada");
}

Console.WriteLine("PASS: checks offline completos. La inferencia Azure requiere una prueba independiente.");

static void Assert(bool condition, string name)
{
    if (!condition) { throw new InvalidOperationException($"FAIL: {name}"); }
    Console.WriteLine($"PASS: {name}");
}

static void Expect<TException>(Action action, string name) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        Console.WriteLine($"PASS: {name}");
        return;
    }
    throw new InvalidOperationException($"FAIL: {name}");
}
