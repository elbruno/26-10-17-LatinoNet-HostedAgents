// Configuracion compartida de 20 a 23: fallar antes de usar valores incompletos o inseguros.
// A. Exigir variables no vacias, sin registrar sus valores.
// B. Validar URLs absolutas HTTPS de recursos configurados por el backend.
// C. Rechazar credenciales, query y fragmento para evitar secretos en URLs.
using Microsoft.Extensions.Configuration;

namespace LatinoNet.Hosted;

internal static class HostedSettings
{
    // A. El mensaje de error nombra la clave, no su valor.
    public static string Required(IConfiguration configuration, string name) =>
        !string.IsNullOrWhiteSpace(configuration[name])
            ? configuration[name]!
            : throw new InvalidOperationException($"{name} is required.");

    public static string Deployment(IConfiguration configuration) =>
        configuration["AZURE_AI_MODEL_DEPLOYMENT_NAME"] is null
            ? "gpt-6-luna"
            : Required(configuration, "AZURE_AI_MODEL_DEPLOYMENT_NAME");

    public static Uri HttpsUri(IConfiguration configuration, string name)
    {
        var value = Required(configuration, name);
        // B/C. El modelo nunca proporciona estas URLs como argumentos de una tool.
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length != 0 ||
            uri.Query.Length != 0 || uri.Fragment.Length != 0)
        {
            throw new InvalidOperationException($"{name} must be an HTTPS URL without credentials, query or fragment.");
        }
        return uri;
    }
}
