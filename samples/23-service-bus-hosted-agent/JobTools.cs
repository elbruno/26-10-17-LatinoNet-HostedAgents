// Estado de demo de 23: propuestas inmutables en memoria y envio tras aprobacion.
// A. Validar el tema y crear una propuesta con caducidad, sin enviar nada.
// B. Permitir revisar y confirmar desde la aplicacion, con exclusion mutua.
// C. Verificar el bearer token fuera del modelo y reutilizar el ID al reintentar.
using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LatinoNet.Jobs;

public sealed class JobTools(Func<string, string, CancellationToken, Task> send, TimeProvider? clock = null)
{
    private readonly Dictionary<string, JobProposal> _pending = [];
    private readonly SemaphoreSlim _gate = new(1);
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    [Description("Propone un trabajo ficticio. NO lo envia. Un humano debe confirmar desde la aplicacion fuera del chat.")]
    public async Task<JobProposal> ProposeAsync(
        [Description("Tema del resumen, entre 1 y 200 caracteres.")] string topic,
        CancellationToken cancellationToken = default)
    {
        // A. Se guarda una propuesta pendiente, no una orden autorizada por el modelo.
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        if (topic.Length > 200 || topic.Any(char.IsControl))
            throw new ArgumentException("Topic must contain 1 to 200 printable characters.", nameof(topic));
        await _gate.WaitAsync(cancellationToken);
        try
        {
            foreach (var expired in _pending.Values.Where(p => p.ExpiresAt <= _clock.GetUtcNow()).ToArray())
                _pending.Remove(expired.Id);
            if (_pending.Count >= 100) throw new InvalidOperationException("Too many pending proposals.");
            var proposal = new JobProposal(Guid.NewGuid().ToString("N"), topic.Trim(), _clock.GetUtcNow().AddMinutes(5), "awaiting-human-confirmation");
            _pending.Add(proposal.Id, proposal);
            return proposal;
        }
        finally { _gate.Release(); }
    }

    // B. El humano revisa datos autoritativos del backend, no solo el texto generado.
    public async Task<JobProposal?> GetProposalAsync(string id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return _pending.TryGetValue(id, out var proposal) && proposal.ExpiresAt > _clock.GetUtcNow()
                ? proposal : null;
        }
        finally { _gate.Release(); }
    }

    // B. No se expone como AIFunction. Un fallo de envio se propaga y conserva la propuesta.
    public async Task<bool> ConfirmAsync(string id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!_pending.TryGetValue(id, out var proposal)) return false;
            if (proposal.ExpiresAt <= _clock.GetUtcNow())
            {
                _pending.Remove(id);
                return false;
            }
            var payload = JsonSerializer.Serialize(new { type = "demo-summary", proposal.Id, proposal.Topic });
            await send(proposal.Id, payload, cancellationToken);
            _pending.Remove(id);
            return true;
        }
        finally { _gate.Release(); }
    }

    // C. La comparacion no revela diferencias de prefijo mediante tiempos de respuesta.
    public static bool IsAuthorized(string? authorization, string expectedToken)
    {
        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer ", StringComparison.Ordinal))
            return false;
        var supplied = SHA256.HashData(Encoding.UTF8.GetBytes(authorization[7..]));
        var expected = SHA256.HashData(Encoding.UTF8.GetBytes(expectedToken));
        return CryptographicOperations.FixedTimeEquals(supplied, expected);
    }
}

public sealed record JobProposal(string Id, string Topic, DateTimeOffset ExpiresAt, string Status);
