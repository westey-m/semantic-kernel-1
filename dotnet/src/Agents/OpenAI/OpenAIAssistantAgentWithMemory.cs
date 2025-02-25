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
    private readonly string _threadId;
    private readonly MemoryManager _memoryManager;
    private readonly bool _loadContextOnFirstMessage;
    private bool _isFirstMessage = true;

    public OpenAIAssistantAgentWithMemory(
        OpenAIAssistantAgent agent,
        string threadId,
        MemoryManager memoryManager,
        bool loadContextOnFirstMessage = true)
    {
        this._agent = agent;
        this._threadId = threadId;
        this._memoryManager = memoryManager;
        this._loadContextOnFirstMessage = loadContextOnFirstMessage;
    }

    public OpenAIAssistantAgentWithMemory(
        OpenAIAssistantAgent agent,
        string threadId,
        bool loadContextOnFirstMessage = true)
    {
        this._agent = agent;
        this._threadId = threadId;
        this._memoryManager = new MemoryManager();
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

        await this._agent.AddChatMessageAsync(this._threadId, chatMessageContent, cancellationToken).ConfigureAwait(false);

        var overrideKernel = this._agent.Kernel.Clone();
        this.MemoryManager.RegisterPlugins(overrideKernel);

        await foreach (ChatMessageContent response in this._agent.InvokeAsync(
            this._threadId,
            new RunCreationOptions { AdditionalInstructions = memoryContext },
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
