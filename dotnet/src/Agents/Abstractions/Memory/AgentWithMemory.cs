// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.Agents.Memory;

public abstract class AgentWithMemory
{
    public abstract MemoryManager MemoryManager { get; }

    public abstract IAsyncEnumerable<ChatMessageContent> CompleteAsync(
        ChatMessageContent chatMessageContent,
        CancellationToken cancellationToken = default);

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
}
