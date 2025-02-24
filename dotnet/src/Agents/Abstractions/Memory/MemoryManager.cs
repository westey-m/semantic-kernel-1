// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel.Agents.Memory;

public abstract class MemoryManager
{
    private readonly List<MemoryComponent> _memoryComponents = new();

    public MemoryManager(Func<ChatHistory> chatHistoryRetriever)
    {
        this.ChatHistoryReriever = chatHistoryRetriever;
    }

    protected Func<ChatHistory> ChatHistoryReriever { get; private set; }

    public ChatHistory ChatHistory => this.ChatHistoryReriever();

    public virtual IReadOnlyList<MemoryComponent> MemoryComponents => this._memoryComponents;

    public virtual void RegisterMemoryComponent(MemoryComponent agentMemory)
    {
        this._memoryComponents.Add(agentMemory);
    }

    public virtual async Task LoadContextAsync(string userInput, CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(this.MemoryComponents.Select(x => x.LoadContextAsync(userInput, cancellationToken)).ToList()).ConfigureAwait(false);
    }

    public virtual async Task SaveContextAsync(CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(this.MemoryComponents.Select(x => x.SaveContextAsync(this.ChatHistoryReriever(), cancellationToken)).ToList()).ConfigureAwait(false);
    }

    public virtual async Task MaintainContextAsync(ChatMessageContent newMessage, CancellationToken cancellationToken = default)
    {
        var renderedContext = string.Empty;
        foreach (var memoryComponent in this.MemoryComponents)
        {
            await memoryComponent.MaintainContextAsync(newMessage, this.ChatHistoryReriever(), cancellationToken).ConfigureAwait(false);
        }
    }

    public virtual async Task<string> GetRenderedContextAsync(CancellationToken cancellationToken = default)
    {
        var renderedContext = string.Empty;
        foreach (var memoryComponent in this.MemoryComponents)
        {
            renderedContext += await memoryComponent.GetRenderedContextAsync(cancellationToken).ConfigureAwait(false) + "\n";
        }

        return renderedContext;
    }
}
