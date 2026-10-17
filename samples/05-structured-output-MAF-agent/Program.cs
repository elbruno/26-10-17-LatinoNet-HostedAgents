// 05: obtener salida tipada y separar el esquema JSON de las reglas de negocio.
// A. Configurar y construir el agente.
// B. Solicitar RunAsync<TalkPlan>, no solamente pedir JSON en un prompt.
// C. Validar semanticamente el resultado antes de usarlo.
using System.Text.Json;
using Azure.AI.Projects;
using Azure.Identity;
using LatinoNet.Demos;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;

// A. El deployment debe soportar structured output.
var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();
var endpoint = configuration["FOUNDRY_PROJECT_ENDPOINT"]
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is not set.");
var deployment = configuration["AZURE_AI_MODEL_DEPLOYMENT_NAME"]
    ?? "gpt-6-luna";

AIAgent agent = new AIProjectClient(new Uri(endpoint), new AzureCliCredential())
    .AsAIAgent(model: deployment, instructions: "Extrae solo los datos presentes en la solicitud.", name: "StructuredAgent");

// B. El tipo proporciona el contrato estructurado.
var response = await agent.RunAsync<TalkPlan>(
    "Titulo: De localhost a Foundry. Duracion: 45 minutos. Temas: C#, Responses, Hosted Agents.");
// C. Un JSON correcto puede seguir siendo un plan de negocio invalido.
response.Result.Validate();
Console.WriteLine(JsonSerializer.Serialize(response.Result, new JsonSerializerOptions { WriteIndented = true }));
