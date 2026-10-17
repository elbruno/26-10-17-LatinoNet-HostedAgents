// 02: mostrar la respuesta incremental del mismo tipo de agente de consola.
// A. Leer configuracion de Foundry.
// B. Crear un agente con instrucciones breves.
// C. Consumir actualizaciones de streaming sin esperar todo el texto.
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;

// A. La configuracion es local; la inferencia sigue usando Foundry.
var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();
var endpoint = configuration["FOUNDRY_PROJECT_ENDPOINT"]
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is not set.");
var deployment = configuration["AZURE_AI_MODEL_DEPLOYMENT_NAME"]
    ?? "gpt-6-luna";

// B. El agente no necesita otro framework para devolver streaming.
AIAgent agent = new AIProjectClient(new Uri(endpoint), new AzureCliCredential())
    .AsAIAgent(model: deployment, instructions: "Responde en espanol, en tres frases breves.", name: "StreamingAgent");

// C. Cada update puede contener solo un fragmento, no una respuesta final completa.
await foreach (var update in agent.RunStreamingAsync("Que cambia al pasar de consola a un servicio alojado?"))
{
    Console.Write(update.Text);
}
Console.WriteLine();
