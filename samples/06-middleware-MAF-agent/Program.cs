// 06: envolver la ejecucion del agente sin modificar su respuesta.
// A. Configurar y construir el agente.
// B. Agregar middleware de medicion sin registrar contenido.
// C. Ejecutar a traves del agente envuelto.
using Azure.AI.Projects;
using Azure.Identity;
using LatinoNet.Demos;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;

// A. El agente conserva su configuracion y credencial.
var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();
var endpoint = configuration["FOUNDRY_PROJECT_ENDPOINT"]
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is not set.");
var deployment = configuration["AZURE_AI_MODEL_DEPLOYMENT_NAME"]
    ?? "gpt-6-luna";

AIAgent agent = new AIProjectClient(new Uri(endpoint), new AzureCliCredential())
    .AsAIAgent(model: deployment, instructions: "Responde en espanol, brevemente.", name: "MiddlewareAgent");

// B. El wrapper mide intentos, no certifica que sean exitosos.
var timedAgent = TimingMiddleware.Wrap(agent, Console.WriteLine);
// C. La llamada usa el pipeline de middleware.
Console.WriteLine(await timedAgent.RunAsync("Que responsabilidad conserva el equipo al alojar su agente?"));
