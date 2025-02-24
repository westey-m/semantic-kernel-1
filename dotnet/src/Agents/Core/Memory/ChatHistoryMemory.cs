// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel.Agents.Memory;

public class ChatHistoryMemory : AgentsMemory
{
    private readonly Kernel _kernel;
    private readonly MemoryDocumentStore _memoryDocumentStore;
    private readonly ChatHistory _chatHistory = new();

    public ChatHistoryMemory(Kernel kernel, string? userPreferencesStoreName = "ChatHistoryStore")
    {
        this._kernel = kernel;
        this._memoryDocumentStore = new OptionalDocumentStore(kernel, userPreferencesStoreName);
    }

    /// <summary>
    /// Gets or sets an optional history reducer to use for keeping this history size constrained.
    /// </summary>
    public IChatHistoryReducer? HistoryReducer { get; init; }

    /// <summary>
    /// Gets the current chat history as maintained by this memory component.
    /// </summary>
    public ChatHistory Chathistory => this._chatHistory;

    /// <summary>
    /// Gets or sets the prompt template to use when generating a summary of the conversation to save to memory at the end of the conversation.
    /// </summary>
    public string SaveSummaryPromptTemplate { get; init; } =
        """
        Please summarise the following conversation in a single paragraph:
        {{$conversation}}
        """;

    /// <inheritdoc/>
    public override async Task LoadContextAsync(string? inputText = default, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("- ChatHistoryMemory - Loading relevant summaries of previous conversations");

        if (!string.IsNullOrWhiteSpace(inputText))
        {
            var memories = await this._memoryDocumentStore.SearchForMatchingMemories(inputText ?? string.Empty, cancellationToken).ToListAsync(cancellationToken).ConfigureAwait(false);
            string allMemories = string.Join(Environment.NewLine, memories);
            string systemMessage = $"This is what was discussed previously:\n{allMemories}";

            // TODO: Merge memory of previous conversations with already loaded memories if called a second time instead
            this._chatHistory.Add(new ChatMessageContent(AuthorRole.System, systemMessage));

            if (!string.IsNullOrWhiteSpace(allMemories))
            {
                Console.WriteLine($"    {allMemories}");
            }
        }
    }

    /// <inheritdoc/>
    public override async Task MaintainContextAsync(ChatMessageContent newMessage, ChatHistory currentChatHistory, CancellationToken cancellationToken = default)
    {
        this._chatHistory.Add(newMessage);
        await this._chatHistory.ReduceInPlaceAsync(this.HistoryReducer, cancellationToken).ConfigureAwait(false);

        Console.WriteLine("- ChatHistoryMemory - Performed maintainence on chat history.");
        Console.WriteLine($"    Added message to history: {newMessage.Content}");
    }

    /// <inheritdoc/>
    public override Task<string> GetRenderedContextAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(string.Empty);
    }

    /// <inheritdoc/>
    public override async Task SaveContextAsync(ChatHistory currentChatHistory, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("- ChatHistoryMemory - Saving summary of this conversation");

        var conversation = string.Join("\n", currentChatHistory
            .Where(x => x.Role == AuthorRole.User || x.Role == AuthorRole.Assistant)
            .Select(x => $"{x.Source ?? x.Role}: {x.Content}")).Trim();

        var result = await this._kernel.InvokePromptAsync(
            this.SaveSummaryPromptTemplate,
            new KernelArguments() { ["conversation"] = conversation },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var summary = result.ToString();

        await this._memoryDocumentStore.SaveMemoryAsync(summary, cancellationToken).ConfigureAwait(false);

        Console.WriteLine($"    {summary}");
    }
}
