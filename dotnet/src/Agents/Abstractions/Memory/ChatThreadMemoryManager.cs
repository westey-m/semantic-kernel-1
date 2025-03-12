// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;
using System.Threading;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Collections.Generic;
using System;

namespace Microsoft.SemanticKernel.Agents.Memory;

public class ChatThreadMemoryManager : MemoryManager
{
    private readonly ChatThread _chatThread;
    private readonly bool _startNewThreadOnFirstMessage;

    public ChatThreadMemoryManager(
        ChatThread chatThread,
        IEnumerable<MemoryComponent>? memoryComponents = default,
        bool startNewThreadOnFirstMessage = true)
    {
        this._chatThread = chatThread;

        if (memoryComponents != null)
        {
            foreach (var memoryComponent in memoryComponents)
            {
                this.RegisterMemoryComponent(memoryComponent);
            }
        }
    }

    /// <inheritdoc/>
    public async Task<string> StartNewThreadAsync(CancellationToken cancellationToken = default)
    {
        var threadId = await this._chatThread.StartNewThreadAsync(cancellationToken).ConfigureAwait(false);
        await this.OnThreadStartAsync(threadId, string.Empty, cancellationToken).ConfigureAwait(false);
        return threadId;
    }

    /// <inheritdoc/>
    public async Task EndThreadAsync(CancellationToken cancellationToken = default)
    {
        if (this._chatThread.HasActiveThread)
        {
            await this.OnThreadEndAsync(this._chatThread.CurrentThreadId!, cancellationToken).ConfigureAwait(false);
        }

        await this._chatThread.EndThreadAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task OnNewMessageAsync(ChatMessageContent newMessage, CancellationToken cancellationToken = default)
    {
        // Check if we need to start a new thread.
        if (!this._chatThread.HasActiveThread)
        {
            if (!this._startNewThreadOnFirstMessage)
            {
                throw new InvalidOperationException("No thread active.");
            }

            await this.StartNewThreadAsync(cancellationToken).ConfigureAwait(false);
        }

        // Notify all registered components of the new message.
        await Task.WhenAll(
        [
            base.OnNewMessageAsync(newMessage, cancellationToken),
            this._chatThread?.OnNewMessageAsync(newMessage, cancellationToken) ?? Task.CompletedTask,
        ]).ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves the current chat history.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The chat history.</returns>
    public Task<ChatHistory> RetrieveCurrentChatHistoryAsync(CancellationToken cancellationToken = default)
    {
        return this._chatThread.RetrieveCurrentChatHistoryAsync(cancellationToken);
    }

    /// <inheritdoc />
    public bool HasActiveThread => this._chatThread.HasActiveThread;

    /// <summary>
    /// Gets the thread id of the currently active thread or null if none is active.
    /// </summary>
    public string? CurrentThreadId => this._chatThread.CurrentThreadId;
}
