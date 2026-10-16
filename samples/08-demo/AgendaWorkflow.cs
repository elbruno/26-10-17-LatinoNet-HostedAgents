using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace LatinoNet.Demos;

public static class AgendaWorkflow
{
    public static AIAgent Create(AIAgent writer, AIAgent reviewer) =>
        AgentWorkflowBuilder.BuildSequential(writer, reviewer).AsAIAgent();
}
