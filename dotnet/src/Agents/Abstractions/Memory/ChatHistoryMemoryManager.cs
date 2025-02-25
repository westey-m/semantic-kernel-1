// Copyright (c) Microsoft. All rights reserved.

using System;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel.Agents.Memory;

public class ChatHistoryMemoryManager : MemoryManager
{
    public ChatHistoryMemoryManager(Func<ChatHistory> chatHistoryRetriever)
    {
        this.ChatHistoryRetriever = chatHistoryRetriever;
    }

    public ChatHistoryMemoryManager(BaseChatHistoryMemoryComponent chatHistoryMemoryComponent)
    {
        this.ChatHistoryRetriever = () => chatHistoryMemoryComponent.Chathistory;
        this.RegisterMemoryComponent(chatHistoryMemoryComponent);
    }

    protected Func<ChatHistory> ChatHistoryRetriever { get; private set; }

    public ChatHistory ChatHistory => this.ChatHistoryRetriever();
}
