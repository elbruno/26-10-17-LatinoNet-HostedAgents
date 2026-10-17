// Tool de 21: buscar documentos autorizados y devolver extractos con referencias.
// A. Construir un filtro OData con escape, usando un grupo confiable del backend.
// B. Limitar la consulta, campos y cantidad de resultados.
// C. Validar y acotar los datos y URLs que recibira el modelo.
using System.ComponentModel;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;

namespace LatinoNet.Search;

public sealed class DocumentSearchTool(SearchClient client, string allowedGroup)
{
    public static string AccessFilter(string group)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(group);
        // A. Interpolar mediante SearchFilter escapa comillas; no concatenar OData del usuario.
        return SearchFilter.Create($"group_ids/any(g: g eq {group})");
    }

    [Description("Busca hasta tres documentos autorizados para el grupo fijo de esta demo. Devuelve extractos y referencias.")]
    public async Task<IReadOnlyList<SearchExcerpt>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        if (query.Length > 300)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "Query must not exceed 300 characters.");
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        cancellationToken = timeout.Token;
        // B. Ningun argumento del modelo puede reemplazar este filtro de acceso.
        var options = new SearchOptions { Size = 3, Filter = AccessFilter(allowedGroup), QueryType = SearchQueryType.Simple };
        foreach (var field in new[] { "id", "title", "content", "url" }) options.Select.Add(field);
        var response = await client.SearchAsync<SearchDocument>(query, options, cancellationToken);
        // C. Las referencias provienen del indice; no se inventan a partir de la respuesta del modelo.
        var excerpts = new List<SearchExcerpt>();
        await foreach (var result in response.Value.GetResultsAsync().WithCancellation(cancellationToken))
        {
            var document = result.Document;
            var url = Field(document, "url", 2048);
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length != 0)
            {
                throw new InvalidOperationException("Search index contains an invalid source URL.");
            }
            excerpts.Add(new SearchExcerpt(Field(document, "id", 128), Field(document, "title", 200),
                Field(document, "content", 2000), uri.AbsoluteUri));
            if (excerpts.Count == 3) break;
        }
        return excerpts;
    }

    private static string Field(SearchDocument document, string name, int limit)
    {
        if (!document.TryGetValue(name, out var raw) || raw is not string value || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Search document is missing {name}.");
        }
        return value.Length <= limit ? value : value[..limit];
    }
}

public sealed record SearchExcerpt(string SourceId, string Title, string Content, string Url);
