// Copyright (c) Microsoft. All rights reserved.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.Agents.Memory;

/// <summary>
/// Base class for all memory components.
/// </summary>
public abstract class MemoryComponent
{
    /// <summary>
    /// Checks long term storage for any memories that are relevant to the current session based on the input text.
    /// </summary>
    /// <param name="inputText">The input text, typically a user ask.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task that completes when the context has been loaded.</returns>
    public virtual Task LoadContextAsync(string? inputText = default, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// This method is called when a new message has been contributed to the chat by any participant.
    /// </summary>
    /// <remarks>
    /// Inheritors can use this method to update their context based on the new message.
    /// </remarks>
    /// <param name="newMessage">The new message.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task that completes when the context has been updated.</returns>
    public virtual Task MaintainContextAsync(ChatMessageContent newMessage, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Saves any relevant data currently in context to long term storage.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task that completes when the context has been saved.</returns>
    public virtual Task SaveContextAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Get the current context as a string that can be passed to a chat completion service as context.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task that completes when the context has been rendered and returned.</returns>
    public abstract Task<string> GetRenderedContextAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Register plugins required by this memory component on the provided <see cref="Kernel"/>.
    /// </summary>
    /// <param name="kernel">The kernel to register the plugins on.</param>
    public virtual void RegisterPlugins(Kernel kernel)
    {
    }
}
