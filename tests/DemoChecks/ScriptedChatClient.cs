using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace LatinoNet.DemoChecks;

internal sealed class ScriptedChatClient(
    Func<IReadOnlyList<ChatMessage>, ChatOptions?, ChatResponse> respond) : IChatClient
{
    public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];
    public ChatOptions? LastOptions { get; private set; }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var request = messages.ToList();
        Requests.Add(request);
        LastOptions = options;
        return Task.FromResult(respond(request, options));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var update in response.ToChatResponseUpdates())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose() { }

    public static ChatResponse Text(string text) => new(new ChatMessage(ChatRole.Assistant, text))
    {
        ResponseId = Guid.NewGuid().ToString(),
        FinishReason = ChatFinishReason.Stop
    };
}
