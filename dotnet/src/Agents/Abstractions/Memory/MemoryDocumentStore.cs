// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.Agents.Memory;

public abstract class MemoryDocumentStore
{
    public abstract Task<string?> GetMemoryAsync(string documentName, CancellationToken cancellationToken = default);

    public abstract IAsyncEnumerable<string> SearchForMatchingMemories(string query, CancellationToken cancellationToken = default);

    public abstract Task SaveMemoryAsync(string documentName, string memoryText, CancellationToken cancellationToken = default);

    public abstract Task SaveMemoryAsync(string memoryText, CancellationToken cancellationToken = default);
}
