// 08: encadenar escritor y revisor mediante un workflow secuencial de MAF.
// A. Configurar Foundry y construir los dos agentes.
// B. Conectarlos en orden, sin delegar el enrutamiento al modelo.
// C. Ejecutar el workflow y mostrar su resultado final.
using Azure.AI.Projects;
using Azure.Identity;
using LatinoNet.Demos;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();
var endpoint = configuration["FOUNDRY_PROJECT_ENDPOINT"]
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is not set.");
var deployment = configuration["AZURE_AI_MODEL_DEPLOYMENT_NAME"]
    ?? "gpt-6-luna";

// A. Cada agente tiene una responsabilidad distinta en el mismo proceso.
var project = new AIProjectClient(new Uri(endpoint), new AzureCliCredential());
AIAgent writer = project.AsAIAgent(
    model: deployment, name: "WriterAgent",
    instructions: "Propone tres bloques para una charla sobre C# y Hosted Agents. Responde en espanol.");
AIAgent reviewer = project.AsAIAgent(
    model: deployment, name: "ReviewerAgent",
    instructions: "Revisa la propuesta anterior. Devuelve una agenda final breve, sin prometer costos cero ni permisos automaticos.");

// B. La salida del escritor es entrada del revisor.
var workflowAgent = AgendaWorkflow.Create(writer, reviewer);
// C. No hay persistencia durable ni dos hosts remotos en este ejemplo.
Console.WriteLine(await workflowAgent.RunAsync("Prepara una agenda introductoria de 45 minutos."));
