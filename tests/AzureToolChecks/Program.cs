// Checks offline de 20 a 23: validaciones de backend, no evidencia de Azure real.
// A. Sustituir transportes y credenciales por implementaciones simuladas.
// B. Verificar limites, referencias y que ningun secreto cruce al modelo.
// C. Probar aprobacion, concurrencia, caducidad, reintentos y cancelacion sin broker.
using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Search.Documents;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using LatinoNet.Blobs;
using LatinoNet.Hosted;
using LatinoNet.Jobs;
using LatinoNet.KeyVault;
using LatinoNet.Search;
using Microsoft.Extensions.Configuration;

static void Check(bool passed, string message)
{
    if (!passed) throw new InvalidOperationException(message);
}
static async Task Reject<T>(Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}

var defaults = new ConfigurationBuilder().AddInMemoryCollection().Build();
Check(HostedSettings.Deployment(defaults) == "gpt-6-luna", "Missing deployment must use gpt-6-luna.");
defaults["AZURE_AI_MODEL_DEPLOYMENT_NAME"] = "explicit-deployment";
Check(HostedSettings.Deployment(defaults) == "explicit-deployment", "Explicit deployment must override the default.");
defaults["AZURE_AI_MODEL_DEPLOYMENT_NAME"] = " ";
await Reject<InvalidOperationException>(() => { HostedSettings.Deployment(defaults); return Task.CompletedTask; });
Console.WriteLine("PASS: deployment default gpt-6-luna, explicit override and invalid configured value.");

var credential = new OfflineCredential();
const string fakeSecret = "offline-test-key";
using var vaultHandler = new Handler(_ => Handler.Json("""{"value":"offline-test-key","id":"https://vault.example/secrets/catalog/version"}"""));
using var vaultHttp = new HttpClient(vaultHandler);
var secretOptions = new SecretClientOptions { Transport = new HttpClientTransport(vaultHttp), DisableChallengeResourceVerification = true };
var secrets = new SecretClient(new Uri("https://vault.example"), credential, secretOptions);
using var backendHandler = new Handler(request =>
{
    Check(request.RequestUri == new Uri("https://catalog.example/quote"), "Model must not control backend URL.");
    Check(request.Headers.GetValues("X-Api-Key").Single() == fakeSecret, "Credential must be used only by backend.");
    return Handler.Json("""{"pricePerPerson":12.50,"untrustedExtra":"offline-test-key"}""");
});
using var backendHttp = new HttpClient(backendHandler);
var catalog = new WorkshopCatalogTool(secrets, "catalog", new Uri("https://catalog.example/quote"), backendHttp);
var budget = await catalog.GetBudgetAsync(4);
Check(budget.Total == 50 && !JsonSerializer.Serialize(budget).Contains(fakeSecret), "Secret or raw backend data escaped into tool output.");
await Reject<ArgumentOutOfRangeException>(() => catalog.GetBudgetAsync(0));
await Reject<ArgumentOutOfRangeException>(() => catalog.GetBudgetAsync(1001));
Check(backendHandler.Count == 1, "Invalid input invoked backend.");
Console.WriteLine("PASS: Key Vault backend credential boundary and input validation (simulated transport).");

Check(DocumentSearchTool.AccessFilter("team' or true") == "group_ids/any(g: g eq 'team'' or true')", "OData group must be escaped.");
using var searchHandler = new Handler(request =>
{
    var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
    using var json = JsonDocument.Parse(body);
    Check(json.RootElement.GetProperty("filter").GetString() == "group_ids/any(g: g eq 'fixed-team')", "Missing backend ACL.");
    Check(json.RootElement.GetProperty("top").GetInt32() == 3, "Search result limit missing.");
    return Handler.Json("""{"value":[{"@search.score":1,"id":"doc-1","title":"Hosted","content":"Referencia de ejemplo","url":"https://docs.example/hosted"}]}""");
});
using var searchHttp = new HttpClient(searchHandler);
var search = new DocumentSearchTool(new SearchClient(new Uri("https://search.example"), "documents", credential,
    new SearchClientOptions { Transport = new HttpClientTransport(searchHttp) }), "fixed-team");
var results = await search.SearchAsync("hosted");
Check(results.Count == 1 && results[0].SourceId == "doc-1", "Missing source references.");
await Reject<ArgumentException>(() => search.SearchAsync(""));
await Reject<ArgumentOutOfRangeException>(() => search.SearchAsync(new string('x', 301)));
Check(searchHandler.Count == 1, "Invalid query invoked Search.");
Console.WriteLine("PASS: Search backend ACL, escaping, references and limits (simulated transport).");

foreach (var invalid in new[] { "../secret.txt", "https://other/a.txt", "a.pdf", "dir/a.txt", "a%2ftxt", "a.txt\n" })
    await Reject<ArgumentException>(() => { BlobDocumentTool.ValidateName(invalid); return Task.CompletedTask; });
BlobDocumentTool.ValidateName("agenda-2026.txt");
Check(await BlobDocumentTool.ReadTextAsync(new MemoryStream(Encoding.UTF8.GetBytes("contenido"))) == "contenido", "UTF-8 read failed.");
Check((await BlobDocumentTool.ReadTextAsync(new MemoryStream(new byte[BlobDocumentTool.MaxBytes]))).Length == BlobDocumentTool.MaxBytes, "Exact byte limit must work.");
await Reject<InvalidOperationException>(() => BlobDocumentTool.ReadTextAsync(new MemoryStream(new byte[BlobDocumentTool.MaxBytes + 1])));
await Reject<DecoderFallbackException>(() => BlobDocumentTool.ReadTextAsync(new MemoryStream([0xff])));
using var blobHandler = new Handler(_ =>
{
    var response = Handler.Json("not text");
    response.Content.Headers.ContentType = new("application/pdf");
    response.Headers.Add("x-ms-blob-type", "BlockBlob");
    return response;
});
using var blobHttp = new HttpClient(blobHandler);
var blob = new BlobDocumentTool(new BlobContainerClient(new Uri("https://storage.example/container"), credential,
    new BlobClientOptions { Transport = new HttpClientTransport(blobHttp) }));
await Reject<InvalidOperationException>(() => blob.ReadAsync("agenda.txt"));
Console.WriteLine("PASS: Blob names, MIME type, strict UTF-8 and 16 KiB boundary (simulated transport).");

var sent = new List<string>();
var clock = new TestClock();
var jobs = new JobTools((id, payload, _) =>
{
    Check(JsonDocument.Parse(payload).RootElement.GetProperty("type").GetString() == "demo-summary", "Invalid queue payload.");
    sent.Add(id);
    return Task.CompletedTask;
}, clock);
var proposal = await jobs.ProposeAsync("Agentes .NET");
Check(sent.Count == 0 && proposal.Status == "awaiting-human-confirmation", "Proposal must not send.");
Check(await jobs.GetProposalAsync(proposal.Id) == proposal, "Application must review authoritative data, not model-rendered text.");
Check(!JobTools.IsAuthorized(null, "private-token") && !JobTools.IsAuthorized("Bearer wrong", "private-token")
    && JobTools.IsAuthorized("Bearer private-token", "private-token"), "Application authentication failed.");
var confirmations = await Task.WhenAll(jobs.ConfirmAsync(proposal.Id), jobs.ConfirmAsync(proposal.Id));
Check(confirmations.Count(x => x) == 1 && sent.SequenceEqual([proposal.Id]), "Concurrent confirmation sent twice.");
Check(await jobs.GetProposalAsync(proposal.Id) is null, "Confirmed proposal must leave the pending review store.");
var expired = await jobs.ProposeAsync("Caduca");
clock.Now = clock.Now.AddMinutes(6);
Check(await jobs.GetProposalAsync(expired.Id) is null, "Expired proposal must not be reviewable.");
Check(!await jobs.ConfirmAsync(expired.Id) && sent.Count == 1, "Expired proposal was sent.");
await Reject<ArgumentException>(() => jobs.ProposeAsync("bad\npayload"));
await Reject<ArgumentException>(() => jobs.ProposeAsync(new string('x', 201)));
var calls = 0;
var retry = new JobTools((id, _, _) => ++calls == 1 ? Task.FromException(new IOException("simulated")) : Task.CompletedTask);
var retryProposal = await retry.ProposeAsync("Retry");
await Reject<IOException>(() => retry.ConfirmAsync(retryProposal.Id));
Check(await retry.ConfirmAsync(retryProposal.Id) && calls == 2, "Failed send must remain retryable with same id.");
using var cancelled = new CancellationTokenSource();
cancelled.Cancel();
await Reject<OperationCanceledException>(() => jobs.ProposeAsync("Cancel", cancelled.Token));
Console.WriteLine("PASS: Service Bus proposal/confirmation separation, concurrency, expiry, retry and cancellation (no broker).");
Console.WriteLine("Offline checks do not certify Azure inference, RBAC, network, deployment or broker delivery.");

sealed class OfflineCredential : TokenCredential
{
    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        new("offline-token", DateTimeOffset.UtcNow.AddHours(1));
    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        ValueTask.FromResult(GetToken(requestContext, cancellationToken));
}

sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public int Count { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Count++;
        return Task.FromResult(respond(request));
    }
    public static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}

sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.Parse("2026-10-17T12:00:00Z");
    public override DateTimeOffset GetUtcNow() => Now;
}
