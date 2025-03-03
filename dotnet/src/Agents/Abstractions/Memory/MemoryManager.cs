// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.Agents.Memory;

public class MemoryManager
{
    private readonly List<MemoryComponent> _memoryComponents = new();

    public virtual IReadOnlyList<MemoryComponent> MemoryComponents => this._memoryComponents;

    public virtual void RegisterMemoryComponent(MemoryComponent agentMemory)
    {
        this._memoryComponents.Add(agentMemory);
    }

    public virtual async Task OnThreadStartAsync(string threadId, string userInput, CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(this.MemoryComponents.Select(x => x.OnThreadStartAsync(threadId, userInput, cancellationToken)).ToList()).ConfigureAwait(false);
    }

    public virtual async Task OnThreadEndAsync(string threadId, CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(this.MemoryComponents.Select(x => x.OnThreadEndAsync(threadId, cancellationToken)).ToList()).ConfigureAwait(false);
    }

    public virtual async Task OnNewMessageAsync(ChatMessageContent newMessage, CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(this.MemoryComponents.Select(x => x.OnNewMessageAsync(newMessage, cancellationToken)).ToList()).ConfigureAwait(false);
    }

    public virtual async Task<string> OnAIInvocationAsync(ChatMessageContent newMessage, CancellationToken cancellationToken = default)
    {
        var subContexts = await Task.WhenAll(this.MemoryComponents.Select(x => x.OnAIInvocationAsync(newMessage, cancellationToken)).ToList()).ConfigureAwait(false);
        return string.Join("\n", subContexts);
    }

    /// <summary>
    /// Register plugins required by all memory components contained by this manager on the provided <see cref="Kernel"/>.
    /// </summary>
    /// <param name="kernel">The kernel to register the plugins on.</param>
    public virtual void RegisterPlugins(Kernel kernel)
    {
        foreach (var memoryComponent in this.MemoryComponents)
        {
            memoryComponent.RegisterPlugins(kernel);
        }
    }
}
