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
    private readonly AzureAIAgentThreadMemoryComponent _openAIAssistantThreadMemoryComponent;
    private readonly AzureAIAgentMemoryManager _memoryManager;
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
        this._openAIAssistantThreadMemoryComponent = new AzureAIAgentThreadMemoryComponent(agent.Client);
        this._memoryManager = new AzureAIAgentMemoryManager(this._openAIAssistantThreadMemoryComponent);
        this._loadContextOnFirstMessage = loadContextOnFirstMessage;
        this._startNewThreadOnFirstMessage = startNewThreadOnFirstMessage;

        foreach (var memoryComponent in memoryComponents)
        {
            this.MemoryManager.RegisterMemoryComponent(memoryComponent);
        }
    }

    public override MemoryManager MemoryManager => this._memoryManager;

    public override bool HasActiveThread => this._openAIAssistantThreadMemoryComponent.HasActiveThread;

    public override Task<string> StartNewThreadAsync(CancellationToken cancellationToken = default)
    {
        return this._openAIAssistantThreadMemoryComponent.StartNewThreadAsync(cancellationToken);
    }
    public override async Task EndThreadAsync(CancellationToken cancellationToken = default)
    {
        await this._memoryManager.SaveContextAsync(cancellationToken).ConfigureAwait(false);
        await this._openAIAssistantThreadMemoryComponent.EndThreadAsync(cancellationToken).ConfigureAwait(false);
    }

    public override async IAsyncEnumerable<ChatMessageContent> CompleteAsync(
        ChatMessageContent chatMessageContent,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!this.HasActiveThread)
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

        await foreach (ChatMessageContent response in this._agent.InvokeAsync(
            this._memoryManager.ThreadId,
            new AzureAIInvocationOptions { AdditionalInstructions = memoryContext },
            new KernelArguments(new PromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() }),
            overrideKernel,
            cancellationToken).ConfigureAwait(false))
        {
            if (response.Role == AuthorRole.Assistant)
            {
                await this._memoryManager.MaintainContextAsync(response, cancellationToken).ConfigureAwait(false);
            }

            yield return response;
        }
    }
}
