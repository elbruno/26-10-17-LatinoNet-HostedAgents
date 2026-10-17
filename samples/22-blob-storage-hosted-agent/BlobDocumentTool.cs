// Tool de 22: leer texto UTF-8 de hasta 16 KiB, sin listar ni modificar blobs.
// A. Rechazar rutas, URLs y nombres que no sean documentos .txt simples.
// B. Descargar desde el contenedor fijo y comprobar MIME y tamano declarado.
// C. Limitar tambien los bytes reales y rechazar UTF-8 invalido.
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using Azure.Storage.Blobs;

namespace LatinoNet.Blobs;

public sealed partial class BlobDocumentTool(BlobContainerClient container)
{
    public const int MaxBytes = 16384;

    [GeneratedRegex(@"\A[A-Za-z0-9][A-Za-z0-9_-]{0,79}\.txt\z", RegexOptions.CultureInvariant)]
    private static partial Regex DocumentName();

    public static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!DocumentName().IsMatch(name))
        {
            throw new ArgumentException("Use a simple .txt document name, without paths or URLs.", nameof(name));
        }
    }

    [Description("Lee un documento UTF-8 .txt de hasta 16 KiB del contenedor fijo autorizado. No lista ni modifica archivos.")]
    public async Task<BlobExcerpt> ReadAsync(string documentName, CancellationToken cancellationToken = default)
    {
        // A. El nombre no puede cambiar de contenedor ni navegar a otras rutas.
        ValidateName(documentName);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        cancellationToken = timeout.Token;
        // B. Solo lectura, con un presupuesto total de 20 segundos.
        var response = await container.GetBlobClient(documentName).DownloadStreamingAsync(cancellationToken: cancellationToken);
        await using var stream = response.Value.Content;
        if (!string.Equals(response.Value.Details.ContentType?.Split(';')[0].Trim(), "text/plain", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only text/plain blobs are allowed.");
        }
        if (response.Value.Details.ContentLength > MaxBytes)
        {
            throw new InvalidOperationException("Document exceeds the 16 KiB limit.");
        }
        return new BlobExcerpt(documentName, await ReadTextAsync(stream, cancellationToken));
    }

    public static async Task<string> ReadTextAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        // C. El byte adicional permite detectar exceso aunque el servidor declare un tamano incorrecto.
        var buffer = new byte[MaxBytes + 1];
        var read = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken);
        if (read > MaxBytes) throw new InvalidOperationException("Document exceeds the 16 KiB limit.");
        return new UTF8Encoding(false, true).GetString(buffer, 0, read).TrimStart('\uFEFF');
    }
}

public sealed record BlobExcerpt(string DocumentName, string Content);
