// Proposito: reutilizar AgendaWorkflow del sample 08 y preservar la cancelacion en el host.
// A. Construir el workflow secuencial con los agentes escritor y revisor.
// B. Comprobar cancelacion antes y despues de ejecutar el workflow.
// C. Devolver un AIAgent que conserva ejecucion normal y streaming.

using LatinoNet.Demos;
using Microsoft.Agents.AI;

namespace LatinoNet.HostedBasics;

public static class HostedAgendaWorkflow
{
    public static AIAgent Create(AIAgent writer, AIAgent reviewer) =>
        AgendaWorkflow.Create(writer, reviewer).AsBuilder()
            .Use(async (messages, session, options, next, cancellationToken) =>
            {
                // B. WorkflowAgent 1.22.0 puede devolver una respuesta vacia con un token ya cancelado.
                cancellationToken.ThrowIfCancellationRequested();
                await next(messages, session, options, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            })
            .Build();
}
