// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.Agents.Memory;

public abstract class AgentWithMemory
{
    public abstract MemoryManager MemoryManager { get; }

    public abstract IAsyncEnumerable<ChatMessageContent> CompleteAsync(
        ChatMessageContent chatMessageContent,
        CancellationToken cancellationToken = default);

    public abstract bool HasActiveThread { get; }

    public abstract Task StartNewThreadAsync(CancellationToken cancellationToken = default);

    public abstract Task EndThreadAsync(CancellationToken cancellationToken = default);
}
