// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.Agents.Memory;

namespace Microsoft.SemanticKernel.Agents.AzureAI;

public class AzureAIAgentMemoryManager : MemoryManager
{
    private readonly AzureAIAgentThreadMemoryComponent _azureAIAgentThreadMemoryComponent;

    public AzureAIAgentMemoryManager(AzureAIAgentThreadMemoryComponent azureAIAgentThreadMemoryComponent)
    {
        this.RegisterMemoryComponent(azureAIAgentThreadMemoryComponent);
        this._azureAIAgentThreadMemoryComponent = azureAIAgentThreadMemoryComponent;
    }

    public string? CurrentThreadId => this._azureAIAgentThreadMemoryComponent.CurrentThreadId;
}
