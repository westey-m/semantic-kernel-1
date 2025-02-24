// Copyright (c) Microsoft. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel.Agents.Memory;

/// <summary>
/// Base class for all memory components.
/// </summary>
public abstract class MemoryComponent
{
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
    /// <param name="currentChatHistory">The current chat history.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task that completes when the context has been updated.</returns>
    public virtual Task MaintainContextAsync(ChatMessageContent newMessage, ChatHistory currentChatHistory, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public virtual Task SaveContextAsync(ChatHistory currentChatHistory, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public abstract Task<string> GetRenderedContextAsync(CancellationToken cancellationToken = default);
}
