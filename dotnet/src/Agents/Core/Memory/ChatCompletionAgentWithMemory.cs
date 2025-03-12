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
    private readonly ChatThreadMemoryManager _memoryManager;

    public ChatCompletionAgentWithMemory(
        ChatCompletionAgent agent,
        IEnumerable<MemoryComponent>? memoryComponents = default,
        bool startNewThreadOnFirstMessage = true)
    {
        this._agent = agent;
        this._memoryManager = new ChatThreadMemoryManager(new ChatHistoryThread(), memoryComponents, startNewThreadOnFirstMessage);
    }

    public ChatCompletionAgentWithMemory(
        ChatCompletionAgent agent,
        ChatThread chatThread,
        IEnumerable<MemoryComponent>? memoryComponents = default,
        bool startNewThreadOnFirstMessage = true)
    {
        this._agent = agent;
        this._memoryManager = new ChatThreadMemoryManager(chatThread, memoryComponents, startNewThreadOnFirstMessage);
    }

    /// <inheritdoc/>
    public override MemoryManager MemoryManager => this._memoryManager;

    /// <inheritdoc/>
    public override bool HasActiveThread => this._memoryManager.HasActiveThread;

    /// <inheritdoc/>
    public override string? CurrentThreadId => this._memoryManager.CurrentThreadId;

    /// <inheritdoc/>
    public override Task<string> StartNewThreadAsync(CancellationToken cancellationToken = default)
    {
        return this._memoryManager.StartNewThreadAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public override async Task EndThreadAsync(CancellationToken cancellationToken = default)
    {
        await this._memoryManager.EndThreadAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<ChatMessageContent> CompleteAsync(
        ChatMessageContent chatMessageContent,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Update the registered components.
        await this._memoryManager.OnNewMessageAsync(chatMessageContent, cancellationToken).ConfigureAwait(false);
        var memoryContext = await this._memoryManager.OnAIInvocationAsync(chatMessageContent, cancellationToken).ConfigureAwait(false);

        // Register plugins.
        var overrideKernel = this._agent.Kernel.Clone();
        this._memoryManager.RegisterPlugins(overrideKernel);

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
                await this._memoryManager.OnNewMessageAsync(response, cancellationToken).ConfigureAwait(false);
            }

            yield return response;
        }
    }
}
