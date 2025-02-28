// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel.Agents.Memory;

public abstract class BaseChatHistoryMemoryComponent : ThreadManagementMemoryComponent
{
    /// <summary>
    /// Gets the current chat history as maintained by this memory component.
    /// </summary>
    public abstract ChatHistory Chathistory { get; }
}
