// Instrumentacion local del ejemplo 09, sin exporter ni captura de contenido.
// A. Identificar la fuente propia de spans.
// B. Envolver el agente con EnableSensitiveData=false.
// C. Escuchar solo esa fuente e imprimir metadatos permitidos.
using System.Diagnostics;
using Microsoft.Agents.AI;

namespace LatinoNet.Demos;

public static class DemoTelemetry
{
    // A. El listener no captura todas las actividades del proceso.
    public const string SourceName = "LatinoNet.Demo09";

    // B. No habilitar prompts ni completions para explicar observabilidad.
    public static AIAgent Wrap(AIAgent agent) =>
        agent.AsBuilder()
            .UseOpenTelemetry(SourceName, options => options.EnableSensitiveData = false)
            .Build();

    // C. El listener local no envia estos datos a un servicio remoto.
    public static ActivityListener Listen(Action<string> log)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => log(
                $"[trace] {activity.OperationName}: {activity.Duration.TotalMilliseconds:F0} ms, estado={activity.Status}, traceId={activity.TraceId}")
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}
