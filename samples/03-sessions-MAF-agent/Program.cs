// 03: comparar historial compartido e historial aislado entre sesiones.
// A. Configurar y construir el agente.
// B. Reutilizar una sesion para dos turnos relacionados.
// C. Crear otra sesion que no comparte el historial anterior.
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;

// A. Usamos la identidad del desarrollador para acceder a Foundry.
var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();
var endpoint = configuration["FOUNDRY_PROJECT_ENDPOINT"]
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is not set.");
var deployment = configuration["AZURE_AI_MODEL_DEPLOYMENT_NAME"]
    ?? "gpt-6-luna";

AIAgent agent = new AIProjectClient(new Uri(endpoint), new AzureCliCredential())
    .AsAIAgent(model: deployment, instructions: "Responde en espanol y de forma breve.", name: "SessionAgent");

// B. El segundo turno recibe el contexto de esta misma sesion.
var session = await agent.CreateSessionAsync();
Console.WriteLine(await agent.RunAsync("Mi charla se llama De localhost a Foundry y dura 45 minutos.", session));
Console.WriteLine(await agent.RunAsync("Como se llama mi charla y cuanto dura?", session));

// C. Esto demuestra aislamiento, no memoria durable implementada por la aplicacion.
var otherSession = await agent.CreateSessionAsync();
Console.WriteLine("Nueva sesion, sin el historial anterior:");
Console.WriteLine(await agent.RunAsync("Que datos te di sobre mi charla en esta conversacion?", otherSession));
