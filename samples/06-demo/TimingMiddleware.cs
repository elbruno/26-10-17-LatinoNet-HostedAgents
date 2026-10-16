using System.Diagnostics;
using Microsoft.Agents.AI;

namespace LatinoNet.Demos;

public static class TimingMiddleware
{
    public static AIAgent Wrap(AIAgent agent, Action<string> log) =>
        agent.AsBuilder()
            .Use(async (messages, session, options, next, cancellationToken) =>
            {
                var stopwatch = Stopwatch.StartNew();
                log("[middleware] Inicio. No se registran prompts ni respuestas.");
                try
                {
                    await next(messages, session, options, cancellationToken);
                }
                finally
                {
                    log($"[middleware] Fin del intento en {stopwatch.ElapsedMilliseconds} ms; no implica exito.");
                }
            })
            .Build();
}
