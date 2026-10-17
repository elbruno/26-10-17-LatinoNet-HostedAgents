// 01: agente minimo de consola. El proceso es local, la inferencia ocurre en Foundry.
// A. Leer endpoint y deployment sin poner sus valores en el codigo.
// B. Construir el AIAgent con la identidad del desarrollador.
// C. Ejecutar una pregunta y mostrar la respuesta real.
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;

// A. Las variables de entorno prevalecen sobre User Secrets.
var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();

var projectEndpoint = new Uri(
    configuration["FOUNDRY_PROJECT_ENDPOINT"]
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is not set."));
var deployment = configuration["AZURE_AI_MODEL_DEPLOYMENT_NAME"]
    ?? throw new InvalidOperationException("AZURE_AI_MODEL_DEPLOYMENT_NAME is not set.");

// B. Este bloque es el que conservaremos al pasar al host del ejemplo 10.
AIAgent agent = new AIProjectClient(projectEndpoint, new AzureCliCredential())
    .AsAIAgent(
        model: deployment,
        instructions: "You are a friendly assistant. Keep your answers brief.",
        name: "LatinoNetHelloAgent");

// C. RunAsync espera una respuesta completa; requiere acceso real al modelo.
Console.WriteLine(await agent.RunAsync("Hola. Explica en una frase que es un agente de IA."));
