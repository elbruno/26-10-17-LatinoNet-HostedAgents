// 04: conectar una funcion C# al agente y comprobar que realmente fue invocada.
// A. Leer la configuracion de Foundry.
// B. Registrar la tool de presupuesto ficticio en AsAIAgent.
// C. Pedir el calculo y comprobar el contador de ejecuciones.
using Azure.AI.Projects;
using Azure.Identity;
using LatinoNet.Demos;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;

// A. No hay credenciales hardcodeadas ni inferencia offline.
var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();
var endpoint = configuration["FOUNDRY_PROJECT_ENDPOINT"]
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is not set.");
var deployment = configuration["AZURE_AI_MODEL_DEPLOYMENT_NAME"]
    ?? "gpt-6-luna";

// B. La funcion valida los argumentos; las instrucciones no reemplazan esa validacion.
var tools = new WorkshopTools();
AIAgent agent = new AIProjectClient(new Uri(endpoint), new AzureCliCredential())
    .AsAIAgent(
        model: deployment,
        instructions: "Responde en espanol. Para calcular el presupuesto usa la herramienta, no calcules de memoria.",
        name: "ToolsAgent",
        tools: tools.CreateTools());

// C. Una respuesta de texto no prueba tool calling: exigimos al menos una invocacion.
Console.WriteLine(await agent.RunAsync("Calcula el presupuesto ficticio para 12 asistentes a 25 por persona."));
Console.WriteLine($"Invocaciones reales de la tool: {tools.InvocationCount}");
if (tools.InvocationCount == 0)
{
    throw new InvalidOperationException("El modelo no invoco la tool. No presentar esta respuesta como una prueba de tool calling.");
}
