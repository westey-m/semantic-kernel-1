// Copyright (c) Microsoft. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel.Agents.Memory;

/// <summary>
/// A base class for conversation threads.
/// </summary>
public abstract class ChatThread
{
    /// <summary>
    /// Gets a value indicating whether a conversation thread is currently active.
    /// </summary>
    public abstract bool HasActiveThread { get; }

    /// <summary>
    /// Gets the id of the current thread.
    /// </summary>
    public abstract string? CurrentThreadId { get; }

    /// <summary>
    /// Starts a new thread and returns the thread id.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The id of the new thread.</returns>
    public abstract Task<string> StartNewThreadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends the current thread.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task that completes when the thread has been ended.</returns>
    public abstract Task EndThreadAsync(CancellationToken cancellationToken = default);

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
    /// Retrieves the current chat history.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The chat history.</returns>
    public abstract Task<ChatHistory> RetrieveCurrentChatHistoryAsync(CancellationToken cancellationToken = default);
}
