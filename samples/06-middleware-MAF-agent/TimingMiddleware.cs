// Middleware del ejemplo 06: medir intentos sin capturar prompts ni respuestas.
// A. Envolver el pipeline y comenzar la medicion.
// B. Invocar next conservando opciones, sesion y cancelacion.
// C. Registrar el fin incluso si hay error, sin ocultar la excepcion.
using System.Diagnostics;
using Microsoft.Agents.AI;

namespace LatinoNet.Demos;

public static class TimingMiddleware
{
    public static AIAgent Wrap(AIAgent agent, Action<string> log) =>
        agent.AsBuilder()
            .Use(async (messages, session, options, next, cancellationToken) =>
            {
                // A. No registrar messages ni contenido del modelo.
                var stopwatch = Stopwatch.StartNew();
                log("[middleware] Inicio. No se registran prompts ni respuestas.");
                try
                {
                    // B. El siguiente componente mantiene la responsabilidad sobre la ejecucion.
                    await next(messages, session, options, cancellationToken);
                }
                finally
                {
                    // C. finally no convierte un fallo en exito.
                    log($"[middleware] Fin del intento en {stopwatch.ElapsedMilliseconds} ms; no implica exito.");
                }
            })
            .Build();
}
