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

    public virtual async Task LoadContextAsync(string userInput, CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(this.MemoryComponents.Select(x => x.LoadContextAsync(userInput, cancellationToken)).ToList()).ConfigureAwait(false);
    }

    public virtual async Task SaveContextAsync(CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(this.MemoryComponents.Select(x => x.SaveContextAsync(cancellationToken)).ToList()).ConfigureAwait(false);
    }

    public virtual async Task MaintainContextAsync(ChatMessageContent newMessage, CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(this.MemoryComponents.Select(x => x.MaintainContextAsync(newMessage, cancellationToken)).ToList()).ConfigureAwait(false);
    }

    public virtual async Task<string> GetFormattedContextAsync(CancellationToken cancellationToken = default)
    {
        var subContexts = await Task.WhenAll(this.MemoryComponents.Select(x => x.GetFormattedContextAsync(cancellationToken)).ToList()).ConfigureAwait(false);
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
