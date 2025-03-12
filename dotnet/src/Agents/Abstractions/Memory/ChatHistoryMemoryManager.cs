// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Collections.Generic;

namespace Microsoft.SemanticKernel.Agents.Memory;

public class ChatHistoryMemoryManager : MemoryManager
{
    private readonly Func<ChatHistory> _chatHistoryRetriever;
    private readonly string _threadId;

    public ChatHistoryMemoryManager(Func<ChatHistory> chatHistoryRetriever, string threadId, IEnumerable<MemoryComponent>? memoryComponents = default)
    {
        this._chatHistoryRetriever = chatHistoryRetriever;
        this._threadId = threadId;

        if (memoryComponents != null)
        {
            foreach (var memoryComponent in memoryComponents)
            {
                this.RegisterMemoryComponent(memoryComponent);
            }
        }
    }

    /// <inheritdoc />
    public override async Task OnNewMessageAsync(ChatMessageContent newMessage, CancellationToken cancellationToken = default)
    {
        var chatHistory = this._chatHistoryRetriever();
        chatHistory.Add(newMessage);
        await base.OnNewMessageAsync(newMessage, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves the current chat history.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The chat history.</returns>
    public Task<ChatHistory> RetrieveCurrentChatHistoryAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(this._chatHistoryRetriever());
    }

    /// <inheritdoc />
    public bool HasActiveThread => true;

    /// <summary>
    /// Gets the thread id of the currently active thread or null if none is active.
    /// </summary>
    public string? CurrentThreadId => this._threadId;
}
