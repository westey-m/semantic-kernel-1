// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using Microsoft.SemanticKernel.Agents.Memory;

namespace Microsoft.SemanticKernel.Agents.OpenAI;

public static class OpenAIAssistantAgentExtensions
{
    public static OpenAIAssistantAgentWithMemory WithMemory(
        this OpenAIAssistantAgent agent,
        IEnumerable<MemoryComponent>? memoryComponents = default,
        bool startNewThreadOnFirstMessage = true)
    {
        return new OpenAIAssistantAgentWithMemory(
            agent,
            memoryComponents,
            startNewThreadOnFirstMessage);
    }
}
