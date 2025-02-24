// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel.Agents.Memory;

public class ChatMemoryManager : AgentsMemoryManager
{
    private readonly List<AgentsMemory> _memoryComponents = new();
    private readonly Func<ChatHistory> _currentChatHistoryReriever;

    public override IReadOnlyList<AgentsMemory> MemoryComponents => this._memoryComponents;

    public ChatHistory ChatHistory => this._currentChatHistoryReriever();

    public ChatMemoryManager(Func<ChatHistory> currentChatHistoryReriever)
    {
        this._currentChatHistoryReriever = currentChatHistoryReriever;
    }

    public ChatMemoryManager(ChatHistoryMemory chatHistoryMemory)
    {
        this._currentChatHistoryReriever = () => chatHistoryMemory.Chathistory;
        this._memoryComponents.Add(chatHistoryMemory);
    }

    public override void RegisterMemoryComponent(AgentsMemory agentMemory)
    {
        this._memoryComponents.Add(agentMemory);
    }

    public override async Task LoadContextAsync(string userInput, CancellationToken cancellationToken = default)
    {
        foreach (var memoryComponent in this.MemoryComponents)
        {
            await memoryComponent.LoadContextAsync(userInput, cancellationToken).ConfigureAwait(false);
        }
        //await Task.WhenAll(this.MemoryComponents.Select(x => x.LoadContextAsync(userInput, cancellationToken)).ToList()).ConfigureAwait(false);
    }

    public override async Task SaveContextAsync(CancellationToken cancellationToken = default)
    {
        foreach (var memoryComponent in this.MemoryComponents)
        {
            await memoryComponent.SaveContextAsync(this._currentChatHistoryReriever(), cancellationToken).ConfigureAwait(false);
        }
        //await Task.WhenAll(this.MemoryComponents.Select(x => x.SaveContextAsync(this._currentChatHistoryReriever(), cancellationToken)).ToList()).ConfigureAwait(false);
    }

    public override async Task MaintainContextAsync(ChatMessageContent newMessage, CancellationToken cancellationToken = default)
    {
        var renderedContext = string.Empty;
        foreach (var memoryComponent in this.MemoryComponents)
        {
            await memoryComponent.MaintainContextAsync(newMessage, this._currentChatHistoryReriever(), cancellationToken).ConfigureAwait(false);
        }
    }
}
