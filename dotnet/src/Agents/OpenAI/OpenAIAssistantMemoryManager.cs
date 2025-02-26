// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.Agents.Memory;

namespace Microsoft.SemanticKernel.Agents.OpenAI;

public class OpenAIAssistantMemoryManager : MemoryManager
{
    private readonly OpenAIAssistantThreadMemoryComponent _openAIAssistantThreadMemoryComponent;

    public OpenAIAssistantMemoryManager(OpenAIAssistantThreadMemoryComponent openAIAssistantThreadMemoryComponent)
    {
        this.RegisterMemoryComponent(openAIAssistantThreadMemoryComponent);
        this._openAIAssistantThreadMemoryComponent = openAIAssistantThreadMemoryComponent;
    }

    public string ThreadId => this._openAIAssistantThreadMemoryComponent.ThreadId;
}
