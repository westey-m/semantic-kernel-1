// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Agents.Memory;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel.Agents.AzureAI;

public class AzureAIAgentWithMemory : AgentWithMemory
{
    private readonly AzureAIAgent _agent;
    private readonly AzureAIAgentChatThread _chatThread;
    private readonly ChatHistoryMemoryManager _memoryManager;
    private readonly bool _loadContextOnFirstMessage;
    private readonly bool _startNewThreadOnFirstMessage;
    private bool _isFirstMessage = true;

    public AzureAIAgentWithMemory(
        AzureAIAgent agent,
        IEnumerable<MemoryComponent>? memoryComponents = default,
        bool loadContextOnFirstMessage = true,
        bool startNewThreadOnFirstMessage = true)
    {
        this._agent = agent;
        this._chatThread = new AzureAIAgentChatThread(agent.Client);
        this._memoryManager = new ChatHistoryMemoryManager(this._chatThread);
        this._loadContextOnFirstMessage = loadContextOnFirstMessage;
        this._startNewThreadOnFirstMessage = startNewThreadOnFirstMessage;

        if (memoryComponents != null)
        {
            foreach (var memoryComponent in memoryComponents)
            {
                this.MemoryManager.RegisterMemoryComponent(memoryComponent);
            }
        }
    }

    /// <inheritdoc />
    public override MemoryManager MemoryManager => this._memoryManager;

    /// <inheritdoc />
    public override bool HasActiveThread => this._chatThread.HasActiveThread;

    /// <inheritdoc/>
    public override string? CurrentThreadId => this._chatThread.CurrentThreadId;

    /// <inheritdoc />
    public override Task<string> StartNewThreadAsync(CancellationToken cancellationToken = default)
    {
        return this._chatThread.StartNewThreadAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override async Task EndThreadAsync(CancellationToken cancellationToken = default)
    {
        if (this.HasActiveThread)
        {
            await this._memoryManager.OnThreadEndAsync(this._chatThread.CurrentThreadId!, cancellationToken).ConfigureAwait(false);
        }

        await this._chatThread.EndThreadAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<ChatMessageContent> CompleteAsync(
        ChatMessageContent chatMessageContent,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Check if we need to start a new thread.
        if (!this.HasActiveThread)
        {
            if (!this._startNewThreadOnFirstMessage)
            {
                throw new InvalidOperationException("No thread active.");
            }

            await this.StartNewThreadAsync(cancellationToken).ConfigureAwait(false);
        }

        // Check if we need to load context.
        if (this._isFirstMessage && this._loadContextOnFirstMessage)
        {
            await this._memoryManager.OnThreadStartAsync(
                this._chatThread.CurrentThreadId!,
                chatMessageContent.Content ?? string.Empty,
                cancellationToken).ConfigureAwait(false);
            this._isFirstMessage = false;
        }

        // Update the registered components.
        await this._memoryManager.OnNewMessageAsync(chatMessageContent, cancellationToken).ConfigureAwait(false);
        var memoryContext = await this._memoryManager.OnAIInvocationAsync(chatMessageContent, cancellationToken).ConfigureAwait(false);

        // Register plugins.
        var overrideKernel = this._agent.Kernel.Clone();
        this.MemoryManager.RegisterPlugins(overrideKernel);

        // Generate the agent response(s)
        await foreach (ChatMessageContent response in this._agent.InvokeAsync(
            this._memoryManager.CurrentThreadId!,
            new AzureAIInvocationOptions { AdditionalInstructions = memoryContext },
            new KernelArguments(new PromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() }),
            overrideKernel,
            cancellationToken).ConfigureAwait(false))
        {
            if (response.Role == AuthorRole.Assistant)
            {
                await this._memoryManager.OnNewMessageAsync(response, cancellationToken).ConfigureAwait(false);
            }

            yield return response;
        }
    }
}
