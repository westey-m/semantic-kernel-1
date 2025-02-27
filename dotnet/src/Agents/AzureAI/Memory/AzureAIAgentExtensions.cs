// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using Microsoft.SemanticKernel.Agents.Memory;

namespace Microsoft.SemanticKernel.Agents.AzureAI;

public static class AzureAIAgentExtensions
{
    public static AzureAIAgentWithMemory WithMemory(
        this AzureAIAgent agent,
        IEnumerable<MemoryComponent>? memoryComponents = default,
        bool loadContextOnFirstMessage = true,
        bool startNewThreadOnFirstMessage = true)
    {
        return new AzureAIAgentWithMemory(
            agent,
            memoryComponents,
            loadContextOnFirstMessage,
            startNewThreadOnFirstMessage);
    }
}
