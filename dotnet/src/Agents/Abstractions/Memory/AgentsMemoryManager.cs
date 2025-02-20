// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.Agents.Memory;

public abstract class AgentsMemoryManager
{
    public abstract IReadOnlyList<AgentsMemory> MemoryComponents { get; }

    public abstract void RegisterMemoryComponent(AgentsMemory agentMemory);

    public abstract Task StartChatAsync(string userInput, CancellationToken cancellationToken = default);

    public abstract Task EndChatAsync(CancellationToken cancellationToken = default);

    public abstract Task MaintainContextAsync(ChatMessageContent newMessage, CancellationToken cancellationToken = default);

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
