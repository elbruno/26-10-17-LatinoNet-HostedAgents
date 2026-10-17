// 09: observar spans locales del agente sin exportarlos a Azure Monitor.
// A. Configurar y construir el agente.
// B. Instalar un listener y envolver el agente con OpenTelemetry.
// C. Ejecutar una pregunta y mostrar metadatos de la traza, no su contenido.
using Azure.AI.Projects;
using Azure.Identity;
using LatinoNet.Demos;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;

// A. La inferencia sigue requiriendo Foundry aunque la observacion sea local.
var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();
var endpoint = configuration["FOUNDRY_PROJECT_ENDPOINT"]
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is not set.");
var deployment = configuration["AZURE_AI_MODEL_DEPLOYMENT_NAME"]
    ?? throw new InvalidOperationException("AZURE_AI_MODEL_DEPLOYMENT_NAME is not set.");

AIAgent agent = new AIProjectClient(new Uri(endpoint), new AzureCliCredential())
    .AsAIAgent(model: deployment, instructions: "Responde en espanol en una frase.", name: "TelemetryAgent");

// B. Esto no configura Application Insights; esa integracion esta en el ejemplo 24.
using var listener = DemoTelemetry.Listen(Console.WriteLine);
var observableAgent = DemoTelemetry.Wrap(agent);
// C. Una traza permite investigar la ejecucion, no evaluar la calidad de la respuesta.
Console.WriteLine(await observableAgent.RunAsync("Para que sirve una traza de agente?"));
