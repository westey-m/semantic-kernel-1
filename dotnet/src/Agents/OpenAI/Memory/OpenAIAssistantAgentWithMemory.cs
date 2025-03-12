// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Agents.Memory;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI.Assistants;

namespace Microsoft.SemanticKernel.Agents.OpenAI;

public class OpenAIAssistantAgentWithMemory : AgentWithMemory
{
    private readonly OpenAIAssistantAgent _agent;
    private readonly ChatThreadMemoryManager _memoryManager;

    public OpenAIAssistantAgentWithMemory(
        OpenAIAssistantAgent agent,
        IEnumerable<MemoryComponent>? memoryComponents = default,
        bool startNewThreadOnFirstMessage = true)
    {
        this._agent = agent;
        this._memoryManager = new ChatThreadMemoryManager(
            new OpenAIAssistantChatThread(agent.Client),
            memoryComponents,
            startNewThreadOnFirstMessage);
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
        this.MemoryManager.RegisterPlugins(overrideKernel);

        // Generate the agent response(s)
        await foreach (ChatMessageContent response in this._agent.InvokeAsync(
            this._memoryManager.CurrentThreadId!,
            new RunCreationOptions { AdditionalInstructions = memoryContext },
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
