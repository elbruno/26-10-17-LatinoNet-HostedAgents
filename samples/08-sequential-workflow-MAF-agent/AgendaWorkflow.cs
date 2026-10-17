// Composicion compartida por 08 y 12: escritor seguido de revisor.
// A. Recibir agentes ya construidos por la aplicacion.
// B. Definir el orden con AgentWorkflowBuilder.BuildSequential.
// C. Adaptar el workflow a AIAgent, compatible con consola o Responses.
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace LatinoNet.Demos;

public static class AgendaWorkflow
{
    // A/B/C. El adaptador no vuelve durable ni distribuido al workflow.
    public static AIAgent Create(AIAgent writer, AIAgent reviewer) =>
        AgentWorkflowBuilder.BuildSequential(writer, reviewer).AsAIAgent();
}
