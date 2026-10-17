// 07: un coordinador usa otro agente como tool, dentro del mismo proceso.
// A. Construir el especialista y contar sus ejecuciones.
// B. Exponerlo al coordinador mediante AsAIFunction.
// C. Pedir una revision y verificar que hubo delegacion real.
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
    ?? "gpt-6-luna";

// A. Son dos agentes de la aplicacion, no dos despliegues remotos independientes.
var project = new AIProjectClient(new Uri(endpoint), new AzureCliCredential());
AIAgent specialist = project.AsAIAgent(
    model: deployment,
    instructions: "Eres un revisor de agendas. Sugiere un unico ajuste concreto, en espanol.",
    name: "AgendaReviewer",
    description: "Revisa la distribucion de minutos de una charla tecnica.");
var delegatedCalls = 0;
var countedSpecialist = specialist.AsBuilder()
    .Use(async (messages, session, options, next, cancellationToken) =>
    {
        Interlocked.Increment(ref delegatedCalls);
        Console.WriteLine("[delegacion] AgendaReviewer ejecutado.");
        await next(messages, session, options, cancellationToken);
    })
    .Build();

// B. La funcion del especialista entra al catalogo de tools del coordinador.
AIAgent coordinator = project.AsAIAgent(
    model: deployment,
    instructions: "Consulta AgendaReviewer antes de responder sobre una agenda. Responde brevemente en espanol.",
    name: "CoordinatorAgent",
    tools: [countedSpecialist.AsAIFunction()]);

// C. El contador evita confundir una respuesta inventada con una delegacion.
Console.WriteLine(await coordinator.RunAsync(
    "Pide al revisor evaluar esta agenda ficticia de 45 minutos: 30 de teoria, 10 de demo y 5 de preguntas."));
if (delegatedCalls == 0)
{
    throw new InvalidOperationException("El coordinador no delego. No presentar esta respuesta como una prueba de agente como tool.");
}
