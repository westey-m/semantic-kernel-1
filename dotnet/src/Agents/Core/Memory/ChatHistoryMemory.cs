// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel.Agents.Memory;

public class ChatHistoryMemory : AgentsMemory
{
    private readonly ChatHistory _chatHistory = new();

    /// <summary>
    /// Gets or sets an optional history reducer to use for keeping this history size constrained.
    /// </summary>
    public IChatHistoryReducer? HistoryReducer { get; init; }

    /// <summary>
    /// Gets the current chat history as maintained by this memory component.
    /// </summary>
    public ChatHistory Chathistory => this._chatHistory;

    /// <inheritdoc/>
    public override Task LoadContextAsync(string? inputText = default, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("- ChatHistoryMemory - Loading relevant summaries of previous conversations");

        // TODO: Load summaries of applicable previous conversation from DB.
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async Task MaintainContextAsync(ChatMessageContent newMessage, ChatHistory currentChatHistory, CancellationToken cancellationToken = default)
    {
        this._chatHistory.Add(newMessage);
        await this._chatHistory.ReduceInPlaceAsync(this.HistoryReducer, cancellationToken).ConfigureAwait(false);

        Console.WriteLine("- ChatHistoryMemory - Performed maintainence on chat history.");
    }

    /// <inheritdoc/>
    public override Task<string> GetRenderedContextAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(string.Empty);
    }

    /// <inheritdoc/>
    public override Task SaveContextAsync(ChatHistory currentChatHistory, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("- ChatHistoryMemory - Saving summary of this conversation");

        // TODO: Summarize conversation and store result in DB.
        return Task.CompletedTask;
    }
}
