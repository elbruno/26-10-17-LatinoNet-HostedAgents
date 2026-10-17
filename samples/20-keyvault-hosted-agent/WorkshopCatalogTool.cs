// Backend de 20: utilizar una credencial de Key Vault para consultar un catalogo.
// A. Validar asistentes y obtener el secreto mediante identidad.
// B. Consultar solo el endpoint fijo, con limites de tiempo y de respuesta.
// C. Proyectar exclusivamente numeros validados hacia el modelo.
using System.ComponentModel;
using System.Net.Http.Json;
using Azure.Security.KeyVault.Secrets;

namespace LatinoNet.KeyVault;

public sealed class WorkshopCatalogTool(SecretClient secrets, string secretName, Uri endpoint, HttpClient http)
{
    [Description("Obtiene un presupuesto ficticio de un catalogo externo. No cobra ni reserva.")]
    public async Task<Budget> GetBudgetAsync(
        [Description("Cantidad de asistentes entre 1 y 1000.")] int attendees,
        CancellationToken cancellationToken = default)
    {
        // A. La identidad de Azure autoriza al backend, no al modelo.
        ArgumentOutOfRangeException.ThrowIfLessThan(attendees, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(attendees, 1000);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        cancellationToken = timeout.Token;
        var secret = await secrets.GetSecretAsync(secretName, cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(secret.Value.Value))
        {
            throw new InvalidOperationException("The catalog credential is empty.");
        }
        // B. La API key viaja en un header HTTPS del backend, nunca en la descripcion de tool.
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new { attendees })
        };
        request.Headers.Add("X-Api-Key", secret.Value.Value);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Catalog request failed with status {(int)response.StatusCode}.");
        }
        if (response.Content.Headers.ContentLength is > 4096)
        {
            throw new InvalidOperationException("Catalog response is too large.");
        }
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[4097];
        var length = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken);
        if (length > 4096)
        {
            throw new InvalidOperationException("Catalog response is too large.");
        }
        // C. Solo el precio numerico validado cruza al modelo, no la credencial ni el JSON crudo.
        using var json = System.Text.Json.JsonDocument.Parse(buffer.AsMemory(0, length));
        var price = json.RootElement.GetProperty("pricePerPerson").GetDecimal();
        if (price is < 0 or > 10000)
        {
            throw new InvalidOperationException("Catalog price is outside the allowed range.");
        }
        return new Budget(attendees, price, decimal.Round(attendees * price, 2), "USD", true);
    }
}

public sealed record Budget(int Attendees, decimal PricePerPerson, decimal Total, string Currency, bool IsDemo);
