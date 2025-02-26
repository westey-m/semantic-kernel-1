// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using Microsoft.SemanticKernel.Agents.Memory;
using OpenAI.Assistants;

namespace Microsoft.SemanticKernel.Agents.OpenAI;

public static class OpenAIAssistantAgentExtensions
{
    public static OpenAIAssistantAgentWithMemory WithMemory(
        this OpenAIAssistantAgent agent,
        AssistantClient client,
        IEnumerable<MemoryComponent>? memoryComponents = default,
        bool loadContextOnFirstMessage = true,
        bool startNewThreadOnFirstMessage = true)
    {
        return new OpenAIAssistantAgentWithMemory(
            agent,
            client,
            memoryComponents,
            loadContextOnFirstMessage,
            startNewThreadOnFirstMessage);
    }
}
