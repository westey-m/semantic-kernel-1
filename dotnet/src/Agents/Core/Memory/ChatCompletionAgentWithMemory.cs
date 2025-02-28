// Copyright (c) Microsoft. All rights reserved.

using System;
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
    private readonly ThreadManagementMemoryComponent _chatHistoryMemoryComponent;
    private readonly ChatHistoryMemoryManager _memoryManager;
    private readonly bool _loadContextOnFirstMessage;
    private readonly bool _startNewThreadOnFirstMessage;
    private bool _isFirstMessage = true;

    public ChatCompletionAgentWithMemory(
        ChatCompletionAgent agent,
        Kernel? chatHistoryMemoryComponentKernel = default,
        IEnumerable<MemoryComponent>? memoryComponents = default,
        bool loadContextOnFirstMessage = true,
        bool startNewThreadOnFirstMessage = true)
    {
        this._agent = agent;
        this._chatHistoryMemoryComponent = new ChatHistoryMemoryComponent(chatHistoryMemoryComponentKernel ?? agent.Kernel);
        this._memoryManager = new ChatHistoryMemoryManager(this._chatHistoryMemoryComponent);
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

    public ChatCompletionAgentWithMemory(
        ChatCompletionAgent agent,
        ThreadManagementMemoryComponent chatHistoryMemoryComponent,
        IEnumerable<MemoryComponent>? memoryComponents = default,
        bool loadContextOnFirstMessage = true,
        bool startNewThreadOnFirstMessage = true)
    {
        this._agent = agent;
        this._chatHistoryMemoryComponent = chatHistoryMemoryComponent;
        this._memoryManager = new ChatHistoryMemoryManager(chatHistoryMemoryComponent);
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

    /// <inheritdoc/>
    public override MemoryManager MemoryManager => this._memoryManager;

    /// <inheritdoc/>
    public override bool HasActiveThread => this._chatHistoryMemoryComponent.HasActiveThread;

    /// <inheritdoc/>
    public override string? CurrentThreadId => this._chatHistoryMemoryComponent.CurrentThreadId;

    /// <inheritdoc/>
    public override Task<string> StartNewThreadAsync(CancellationToken cancellationToken = default)
    {
        return this._chatHistoryMemoryComponent.StartNewThreadAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public override async Task EndThreadAsync(CancellationToken cancellationToken = default)
    {
        if (this._chatHistoryMemoryComponent.HasActiveThread)
        {
            await this._memoryManager.SaveContextAsync(cancellationToken).ConfigureAwait(false);
        }

        await this._chatHistoryMemoryComponent.EndThreadAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<ChatMessageContent> CompleteAsync(
        ChatMessageContent chatMessageContent,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!this._chatHistoryMemoryComponent.HasActiveThread)
        {
            if (!this._startNewThreadOnFirstMessage)
            {
                throw new InvalidOperationException("No thread active.");
            }

            await this.StartNewThreadAsync(cancellationToken).ConfigureAwait(false);
        }

        if (this._isFirstMessage && this._loadContextOnFirstMessage)
        {
            await this._memoryManager.LoadContextAsync(chatMessageContent.Content ?? string.Empty, cancellationToken).ConfigureAwait(false);
            this._isFirstMessage = false;
        }

        await this._memoryManager.MaintainContextAsync(chatMessageContent, cancellationToken).ConfigureAwait(false);
        var memoryContext = await this._memoryManager.GetFormattedContextAsync(cancellationToken).ConfigureAwait(false);

        var overrideKernel = this._agent.Kernel.Clone();
        this.MemoryManager.RegisterPlugins(overrideKernel);

        // Generate the agent response(s)
        var chatHistory = await this._memoryManager.RetrieveCurrentChatHistoryAsync(cancellationToken).ConfigureAwait(false);
        await foreach (ChatMessageContent response in this._agent.InvokeAsync(
            chatHistory,
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
