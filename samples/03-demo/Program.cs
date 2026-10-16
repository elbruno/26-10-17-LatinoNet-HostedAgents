using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();
var endpoint = configuration["FOUNDRY_PROJECT_ENDPOINT"]
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is not set.");
var deployment = configuration["AZURE_AI_MODEL_DEPLOYMENT_NAME"]
    ?? throw new InvalidOperationException("AZURE_AI_MODEL_DEPLOYMENT_NAME is not set.");

AIAgent agent = new AIProjectClient(new Uri(endpoint), new DefaultAzureCredential())
    .AsAIAgent(model: deployment, instructions: "Responde en espanol y de forma breve.", name: "SessionAgent");

var session = await agent.CreateSessionAsync();
Console.WriteLine(await agent.RunAsync("Mi charla se llama De localhost a Foundry y dura 45 minutos.", session));
Console.WriteLine(await agent.RunAsync("Como se llama mi charla y cuanto dura?", session));

var otherSession = await agent.CreateSessionAsync();
Console.WriteLine("Nueva sesion, sin el historial anterior:");
Console.WriteLine(await agent.RunAsync("Que datos te di sobre mi charla en esta conversacion?", otherSession));
