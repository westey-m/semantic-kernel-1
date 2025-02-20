// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel.Agents.Memory;

public class ChatMemoryManager : AgentsMemoryManager
{
    private readonly List<AgentsMemory> _memoryComponents = new();
    private readonly Func<ChatHistory> _currentChatHistoryReriever;

    public override IReadOnlyList<AgentsMemory> MemoryComponents => this._memoryComponents;

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

    public override async Task StartChatAsync(string userInput, CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(this.MemoryComponents.Select(x => x.LoadContextAsync(userInput, cancellationToken)).ToList()).ConfigureAwait(false);
    }

    public override async Task EndChatAsync(CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(this.MemoryComponents.Select(x => x.SaveContextAsync(this._currentChatHistoryReriever(), cancellationToken)).ToList()).ConfigureAwait(false);
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
