// Copyright (c) Microsoft. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel.Agents.Memory;

/// <summary>
/// A base class for memory components that manage conversation threads.
/// </summary>
public abstract class ThreadManagementMemoryComponent : MemoryComponent
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
    /// Retrieves the current chat history.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The chat history.</returns>
    public abstract Task<ChatHistory> RetrieveCurrentChatHistoryAsync(CancellationToken cancellationToken = default);
}
