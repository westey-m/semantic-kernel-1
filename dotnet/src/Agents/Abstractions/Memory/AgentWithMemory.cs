// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Threading;

namespace Microsoft.SemanticKernel.Agents.Memory;

public abstract class AgentWithMemory
{
    public abstract MemoryManager MemoryManager { get; }

    public abstract IAsyncEnumerable<ChatMessageContent> InvokeAsync(
        ChatMessageContent chatMessageContent,
        CancellationToken cancellationToken = default);
}
