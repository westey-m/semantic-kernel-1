// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel.Agents.Memory;

/// <summary>
/// An agent that has integrated memory components attached.
/// </summary>
public class ChatCompletionAgentWithMemory : AgentWithMemory
{
    private readonly ChatCompletionAgent _agent;
    private readonly ChatHistoryMemoryManager _memoryManager;
    private readonly bool _loadContextOnFirstMessage;
    private bool _isFirstMessage = true;

    public ChatCompletionAgentWithMemory(
        ChatCompletionAgent agent,
        ChatHistoryMemoryManager memoryManager,
        bool loadContextOnFirstMessage = true)
    {
        this._agent = agent;
        this._memoryManager = memoryManager;
        this._loadContextOnFirstMessage = loadContextOnFirstMessage;
    }

    public ChatCompletionAgentWithMemory(
        ChatCompletionAgent agent,
        ChatHistoryMemoryComponent chatHistoryMemoryComponent,
        bool loadContextOnFirstMessage = true)
    {
        this._agent = agent;
        this._memoryManager = new ChatHistoryMemoryManager(chatHistoryMemoryComponent);
        this._loadContextOnFirstMessage = loadContextOnFirstMessage;
    }

    public override MemoryManager MemoryManager => this._memoryManager;

    public override async IAsyncEnumerable<ChatMessageContent> InvokeAsync(
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

        var overrideKernel = this._agent.Kernel.Clone();
        this.MemoryManager.RegisterPlugins(overrideKernel);

        // Generate the agent response(s)
        await foreach (ChatMessageContent response in this._agent.InvokeAsync(
            this._memoryManager.ChatHistory,
            new KernelArguments(new PromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() }),
            overrideInstructions: memoryContext,
            overrideKernel,
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
