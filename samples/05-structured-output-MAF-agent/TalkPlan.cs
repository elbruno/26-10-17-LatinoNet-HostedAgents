// Contrato del ejemplo 05: plan tipado con validacion posterior al esquema.
// A. Declarar titulo, minutos y temas.
// B. Validar que los campos tienen sentido para esta demo.
// C. Propagar un error explicito si el plan no cumple las reglas.
namespace LatinoNet.Demos;

// A. Estos campos determinan la forma de la salida estructurada.
public sealed record TalkPlan(string Title, int Minutes, string[] Topics)
{
    public void Validate()
    {
        // B. El esquema no sustituye esta validacion semantica.
        if (string.IsNullOrWhiteSpace(Title) || Minutes is < 1 or > 120 ||
            Topics is null || Topics.Length == 0 || Topics.Any(string.IsNullOrWhiteSpace))
        {
            // C. No convertir un resultado invalido en una respuesta de exito.
            throw new InvalidOperationException("El plan no cumple las reglas: titulo, duracion de 1 a 120 y al menos un tema no vacio.");
        }
    }
}
