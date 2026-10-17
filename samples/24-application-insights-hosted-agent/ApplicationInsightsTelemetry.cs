// Proposito: centralizar instrumentacion MAF/HTTP y una politica conservadora de privacidad.
// A. Exigir configuracion y envolver el agente con el ActivitySource propio del sample.
// B. Registrar sanitizacion, ASP.NET Core, HttpClient y el exporter cuando el host no lo agrega.
// C. Antes de exportar, conservar solo metadatos numericos seguros y descartar spans con eventos/links.

using Azure.Monitor.OpenTelemetry.Exporter;
using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Trace;
using OpenTelemetry;

namespace LatinoNet.HostedBasics;

public static class ApplicationInsightsTelemetry
{
    // A. Esta fuente identifica los spans creados por el wrapper MAF.
    public const string SourceName = "LatinoNet.HostedAgent24";

    public static string RequireConnectionString(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"])
            ? configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]!
            : throw new InvalidOperationException("APPLICATIONINSIGHTS_CONNECTION_STRING is not set.");

    public static AIAgent Wrap(AIAgent agent) =>
        agent.AsBuilder()
            .UseOpenTelemetry(SourceName, options => options.EnableSensitiveData = false)
            .Build();

    public static void Configure(
        TracerProviderBuilder tracing,
        string connectionString,
        bool useHostExporter = false,
        Action<AzureMonitorExporterOptions>? configureExporter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        // B. El orden importa: limpiar antes de cualquier exporter, incluido el de AgentHost.Build().
        tracing.AddProcessor(new SafeTraceProcessor())
            .AddSource(SourceName)
            .AddAspNetCoreInstrumentation(options => options.RecordException = false)
            .AddHttpClientInstrumentation(options => options.RecordException = false);

        // El host ya agrega un exporter si la conexion esta en las variables de entorno.
        if (!useHostExporter)
        {
            tracing.AddAzureMonitorTraceExporter(options =>
            {
                options.ConnectionString = connectionString;
                options.DisableOfflineStorage = true;
                options.EnableStandardMetrics = false;
                options.EnablePerformanceCounters = false;
                options.SamplingRatio = 1;
                options.TracesPerSecond = null;
                configureExporter?.Invoke(options);
            });
        }
    }

    public sealed class SafeTraceProcessor : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity activity)
        {
            // C. Activity no permite eliminar eventos ni atributos de links con APIs publicas.
            // Descartar el span evita filtrar excepciones o contenido del modelo en esas estructuras.
            if (activity.Events.Any() || activity.Links.Any())
            {
                activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                return;
            }

            // Nombres y descripciones tambien pueden contener contenido arbitrario del usuario.
            activity.DisplayName = activity.Kind switch
            {
                ActivityKind.Server => "hosted-agent request",
                ActivityKind.Client => "hosted-agent dependency",
                _ => "hosted-agent operation"
            };
            activity.SetStatus(activity.Status);
            foreach (var tag in activity.TagObjects.ToArray())
            {
                // Lista permitida, no lista de secretos: un tag nuevo se elimina por defecto.
                var numericMetadata = tag.Key is "http.response.status_code" or "http.status_code"
                    or "server.port" or "gen_ai.usage.input_tokens" or "gen_ai.usage.output_tokens";
                if (!numericMetadata || tag.Value is not (int or long or double or float))
                {
                    activity.SetTag(tag.Key, null);
                }
            }
            foreach (var baggage in activity.Baggage.ToArray())
            {
                activity.SetBaggage(baggage.Key, null);
            }
        }
    }
}
