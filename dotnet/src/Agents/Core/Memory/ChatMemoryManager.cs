// Copyright (c) Microsoft. All rights reserved.

using System;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel.Agents.Memory;

public class ChatMemoryManager : MemoryManager
{
    public ChatMemoryManager(Func<ChatHistory> chatHistoryRetriever)
        : base(chatHistoryRetriever)
    {
    }

    public ChatMemoryManager(ChatHistoryMemoryComponent chatHistoryMemory)
        : base(() => chatHistoryMemory.Chathistory)
    {
        this.RegisterMemoryComponent(chatHistoryMemory);
    }
}
