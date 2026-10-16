using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace LatinoNet.Demos;

public sealed class WorkshopTools
{
    private int _invocationCount;
    public int InvocationCount => Volatile.Read(ref _invocationCount);

    public IList<AITool> CreateTools() => [AIFunctionFactory.Create(CalculateBudget)];

    [Description("Calcula un presupuesto ficticio de taller. No realiza cobros ni reservas.")]
    public decimal CalculateBudget(
        [Description("Cantidad de asistentes, de 1 a 1000.")] int attendees,
        [Description("Precio por persona, de 0 a 10000.")] decimal pricePerPerson)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attendees, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(attendees, 1000);
        ArgumentOutOfRangeException.ThrowIfNegative(pricePerPerson);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pricePerPerson, 10000m);
        Interlocked.Increment(ref _invocationCount);
        Console.WriteLine("[tool] CalculateBudget ejecutada (datos ficticios).");
        return decimal.Round(attendees * pricePerPerson, 2, MidpointRounding.AwayFromZero);
    }
}
