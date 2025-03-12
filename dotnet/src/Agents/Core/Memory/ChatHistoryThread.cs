// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel.Agents.Memory;

public class ChatHistoryThread : ChatThread
{
    private readonly ChatHistory _chatHistory = new();
    private bool _threadActive = false;
    private string? _threadId;

    public ChatHistoryThread()
    {
    }

    /// <summary>
    /// Gets or sets an optional history reducer to use for keeping this history size constrained.
    /// </summary>
    public IChatHistoryReducer? HistoryReducer { get; init; }

    /// <inheritdoc/>
    public override bool HasActiveThread => this._threadActive;

    /// <inheritdoc/>
    public override string? CurrentThreadId => this._threadId;

    /// <inheritdoc/>
    public override Task<string> StartNewThreadAsync(CancellationToken cancellationToken = default)
    {
        if (this._threadActive)
        {
            throw new InvalidOperationException("Thread already active.");
        }

        this._threadId = Guid.NewGuid().ToString("N");
        this._threadActive = true;

        Console.WriteLine("- ChatHistoryThread - Thread started.");

        return Task.FromResult(this._threadId);
    }

    /// <inheritdoc/>
    public override Task EndThreadAsync(CancellationToken cancellationToken = default)
    {
        if (!this._threadActive)
        {
            throw new InvalidOperationException("No thread active.");
        }

        this._chatHistory.Clear();
        this._threadId = null;
        this._threadActive = false;

        Console.WriteLine("- ChatHistoryThread - Thread ended.");

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task OnNewMessageAsync(ChatMessageContent newMessage, CancellationToken cancellationToken = default)
    {
        this._chatHistory.Add(newMessage);

        Console.WriteLine("- ChatHistoryThread - OnNewMessage."
            + $"\n    Added message to history: {newMessage.Content}");

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task<ChatHistory> RetrieveCurrentChatHistoryAsync(CancellationToken cancellationToken = default)
    {
        if (!this._threadActive)
        {
            throw new InvalidOperationException("No thread active.");
        }

        return Task.FromResult(this._chatHistory);
    }
}
