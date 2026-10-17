// Tool compartida por 04 y 11: calcula un presupuesto ficticio, sin cobrar ni reservar.
// A. Exponer la funcion y sus descripciones como contrato de tool.
// B. Rechazar cantidades y precios fuera del rango permitido.
// C. Contar la ejecucion real y devolver un decimal redondeado.
using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace LatinoNet.Demos;

public sealed class WorkshopTools
{
    private int _invocationCount;
    public int InvocationCount => Volatile.Read(ref _invocationCount);

    // A. El modelo conoce la firma y las descripciones, no decide las reglas de negocio.
    public IList<AITool> CreateTools() => [AIFunctionFactory.Create(CalculateBudget)];

    [Description("Calcula un presupuesto ficticio de taller. No realiza cobros ni reservas.")]
    public decimal CalculateBudget(
        [Description("Cantidad de asistentes, de 1 a 1000.")] int attendees,
        [Description("Precio por persona, de 0 a 10000.")] decimal pricePerPerson)
    {
        // B. Estas reglas se ejecutan en C#, aunque el modelo genere argumentos invalidos.
        ArgumentOutOfRangeException.ThrowIfLessThan(attendees, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(attendees, 1000);
        ArgumentOutOfRangeException.ThrowIfNegative(pricePerPerson);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pricePerPerson, 10000m);
        // C. Solo contamos llamadas que superaron las validaciones.
        Interlocked.Increment(ref _invocationCount);
        Console.WriteLine("[tool] CalculateBudget ejecutada (datos ficticios).");
        return decimal.Round(attendees * pricePerPerson, 2, MidpointRounding.AwayFromZero);
    }
}
