namespace LatinoNet.Demos;

public sealed record TalkPlan(string Title, int Minutes, string[] Topics)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Title) || Minutes is < 1 or > 120 ||
            Topics is null || Topics.Length == 0 || Topics.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("El plan no cumple las reglas: titulo, duracion de 1 a 120 y al menos un tema no vacio.");
        }
    }
}
