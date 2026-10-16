using System.Diagnostics;
using Microsoft.Agents.AI;

namespace LatinoNet.Demos;

public static class DemoTelemetry
{
    public const string SourceName = "LatinoNet.Demo09";

    public static AIAgent Wrap(AIAgent agent) =>
        agent.AsBuilder()
            .UseOpenTelemetry(SourceName, options => options.EnableSensitiveData = false)
            .Build();

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
