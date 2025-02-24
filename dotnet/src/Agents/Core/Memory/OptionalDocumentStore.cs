// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.SemanticKernel.Agents.Memory;

internal class OptionalDocumentStore : MemoryDocumentStore
{
    private readonly MemoryDocumentStore? _memoryDocumentStore;

    public OptionalDocumentStore(Kernel kernel, string? storeName)
    {
        if (storeName is not null)
        {
            this._memoryDocumentStore = kernel.Services.GetKeyedService<MemoryDocumentStore>(storeName);
        }
    }

    /// <inheritdoc/>
    public override Task<string?> GetMemoryAsync(string documentName, CancellationToken cancellationToken = default)
    {
        if (this._memoryDocumentStore is not null)
        {
            return this._memoryDocumentStore.GetMemoryAsync(documentName, cancellationToken);
        }

        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public override Task SaveMemoryAsync(string documentName, string memoryText, CancellationToken cancellationToken = default)
    {
        if (this._memoryDocumentStore is not null)
        {
            return this._memoryDocumentStore.SaveMemoryAsync(documentName, memoryText, cancellationToken);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task SaveMemoryAsync(string memoryText, CancellationToken cancellationToken = default)
    {
        if (this._memoryDocumentStore is not null)
        {
            return this._memoryDocumentStore.SaveMemoryAsync(memoryText, cancellationToken);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override IAsyncEnumerable<string> SearchForMatchingMemories(string query, CancellationToken cancellationToken = default)
    {
        if (this._memoryDocumentStore is not null)
        {
            return this._memoryDocumentStore.SearchForMatchingMemories(query, cancellationToken);
        }

        return AsyncEnumerable.Empty<string>();
    }
}
