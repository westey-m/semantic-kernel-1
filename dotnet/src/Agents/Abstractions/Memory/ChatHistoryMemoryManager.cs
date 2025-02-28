// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel.Agents.Memory;

public class ChatHistoryMemoryManager : MemoryManager
{
    private readonly Func<ChatHistory>? _chatHistoryRetriever;
    private readonly ThreadManagementMemoryComponent _chatHistoryMemoryComponent;

    public ChatHistoryMemoryManager(Func<ChatHistory> chatHistoryRetriever)
    {
        this._chatHistoryRetriever = chatHistoryRetriever;
    }

    public ChatHistoryMemoryManager(ThreadManagementMemoryComponent chatHistoryMemoryComponent)
    {
        this.RegisterMemoryComponent(chatHistoryMemoryComponent);
        this._chatHistoryMemoryComponent = chatHistoryMemoryComponent;
    }

    /// <summary>
    /// Retrieves the current chat history.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The chat history.</returns>
    public Task<ChatHistory> RetrieveCurrentChatHistoryAsync(CancellationToken cancellationToken = default)
    {
        if (this._chatHistoryRetriever != null)
        {
            return Task.FromResult(this._chatHistoryRetriever());
        }

        return this._chatHistoryMemoryComponent.RetrieveCurrentChatHistoryAsync(cancellationToken);
    }
}
