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
    /// Called when a new thread is started.
    /// </summary>
    /// <remarks>
    /// Implementers can use this method to do any operations required at the start of a new thread.
    /// For exmple, checking long term storage for any memories that are relevant to the current session based on the input text.
    /// </remarks>
    /// <param name="inputText">The input text, typically a user ask.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task that completes when the context has been loaded.</returns>
    public virtual Task OnThreadStartAsync(string? inputText = default, CancellationToken cancellationToken = default)
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
    public virtual Task OnNewMessageAsync(ChatMessageContent newMessage, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when a thread is ended.
    /// </summary>
    /// <remarks>
    /// Implementers can use this method to do any operations required at the end of a thread.
    /// For exmple, storing the context to long term storage.
    /// </remarks>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task that completes when the context has been saved.</returns>
    public virtual Task OnThreadEndAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called just before the AI is invoked
    /// Implementers can load any additional context required at this time,
    /// but they should also return any context that should be passed to the AI.
    /// </summary>
    /// <param name="newMessage">The most recent message that the AI is being invoked with.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task that completes when the context has been rendered and returned.</returns>
    public abstract Task<string> OnAIInvocationAsync(ChatMessageContent newMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Register plugins required by this memory component on the provided <see cref="Kernel"/>.
    /// </summary>
    /// <param name="kernel">The kernel to register the plugins on.</param>
    public virtual void RegisterPlugins(Kernel kernel)
    {
    }
}
