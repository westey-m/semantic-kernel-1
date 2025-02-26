// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using Microsoft.SemanticKernel.Agents.Memory;

namespace Microsoft.SemanticKernel.Agents;

public static class ChatCompletionAgentExtensions
{
    public static ChatCompletionAgentWithMemory WithMemory(
        this ChatCompletionAgent agent,
        Kernel? chatHistoryMemoryComponentKernel = default,
        IEnumerable<MemoryComponent>? memoryComponents = default,
        bool loadContextOnFirstMessage = true,
        bool startNewThreadOnFirstMessage = true)
    {
        return new ChatCompletionAgentWithMemory(
            agent,
            chatHistoryMemoryComponentKernel,
            memoryComponents,
            loadContextOnFirstMessage,
            startNewThreadOnFirstMessage);
    }
}
