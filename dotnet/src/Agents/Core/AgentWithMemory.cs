// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Agents.Memory;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel.Agents;

public class AgentWithMemory
{
    private readonly ChatCompletionAgent _agent;
    private readonly MemoryManager _memoryManager;
    private readonly bool _loadContextOnFirstMessage;
    private bool _isFirstMessage = true;

    public AgentWithMemory(ChatCompletionAgent agent, MemoryManager memoryManager, bool loadContextOnFirstMessage = true)
    {
        this._agent = agent;
        this._memoryManager = memoryManager;
        this._loadContextOnFirstMessage = loadContextOnFirstMessage;
    }

    public AgentWithMemory(ChatCompletionAgent agent, ChatHistoryMemoryComponent chatHistoryMemoryComponent, bool loadContextOnFirstMessage = true)
    {
        this._agent = agent;
        this._memoryManager = new ChatMemoryManager(chatHistoryMemoryComponent);
        this._loadContextOnFirstMessage = loadContextOnFirstMessage;
    }

    public MemoryManager MemoryManager => this._memoryManager;

    public async IAsyncEnumerable<ChatMessageContent> InvokeAsync(
        ChatMessageContent chatMessageContent,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (this._isFirstMessage && this._loadContextOnFirstMessage)
        {
            await this._memoryManager.LoadContextAsync(chatMessageContent.Content ?? string.Empty, cancellationToken).ConfigureAwait(false);
            this._isFirstMessage = false;
        }

        await this._memoryManager.MaintainContextAsync(chatMessageContent, cancellationToken).ConfigureAwait(false);
        var memoryContext = await this._memoryManager.GetRenderedContextAsync(cancellationToken).ConfigureAwait(false);

        // Generate the agent response(s)
        await foreach (ChatMessageContent response in this._agent.InvokeAsync(
            this._memoryManager.ChatHistory,
            new KernelArguments(new PromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() }),
            overrideInstructions: memoryContext,
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (response.Role == AuthorRole.Assistant)
            {
                await this._memoryManager.MaintainContextAsync(response, cancellationToken).ConfigureAwait(false);
            }

            yield return response;
        }
    }
}
