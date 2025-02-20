// Copyright (c) Microsoft. All rights reserved.
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.Agents.Memory;
using Microsoft.SemanticKernel.Agents.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.SemanticKernel.Embeddings;

namespace Agents;
/// <summary>
/// Demonstrate that two different agent types are able to participate in the same conversation.
/// In this case a <see cref="ChatCompletionAgent"/> and <see cref="OpenAIAssistantAgent"/> participate.
/// </summary>
public class Agents_Memory(ITestOutputHelper output) : BaseAgentsTest(output)
{
    private const string ReviewerName = "ArtDirector";
    private const string ReviewerInstructions =
        """
        You are an art director who has opinions about copywriting born of a love for David Ogilvy.
        The goal is to determine is the given copy is acceptable to print.
        If so, state that it is approved.
        If not, provide insight on how to refine suggested copy without example.
        """;

    private const string CopyWriterName = "CopyWriter";
    private const string CopyWriterInstructions =
        """
        You are a copywriter with ten years of experience and are known for brevity and a dry humor.
        The goal is to refine and decide on the single best copy as an expert in the field.
        Only provide a single proposal per response.
        You're laser focused on the goal at hand.
        Don't waste time with chit chat.
        Consider suggestions when refining an idea.
        """;

    [Fact]
    public async Task ChatWitTwoChatCompletionAgentsAsync()
    {
        var kernel = CreateKernelWithMemorySupport();

        // Define the agents: one of each type
        ChatCompletionAgent agentReviewer =
            new()
            {
                Instructions = ReviewerInstructions,
                Name = ReviewerName,
                Kernel = kernel,
            };

        ChatCompletionAgent agentWriter =
            new()
            {
                Instructions = CopyWriterInstructions,
                Name = CopyWriterName,
                Kernel = kernel,
            };

        // Create a chat for agent interaction.
        AgentGroupChat chat =
            new(agentWriter, agentReviewer)
            {
                ExecutionSettings =
                    new()
                    {
                        // Here a TerminationStrategy subclass is used that will terminate when
                        // an assistant message contains the term "approve".
                        TerminationStrategy =
                            new ApprovalTerminationStrategy()
                            {
                                // Only the art-director may approve.
                                Agents = [agentReviewer],
                                // Limit total number of turns
                                MaximumIterations = 10,
                            }
                    }
            };

        chat.MemoryManager.RegisterMemoryComponent(new UserPreferencesMemory(kernel));
        await chat.MemoryManager.StartChatAsync("concept: maps made out of egg cartons.");

        // Invoke chat and display messages.
        ChatMessageContent input = new(AuthorRole.User, "concept: maps made out of egg cartons.");
        chat.AddChatMessage(input);
        this.WriteAgentChatMessage(input);

        await foreach (ChatMessageContent response in chat.InvokeAsync())
        {
            this.WriteAgentChatMessage(response);
        }

        await chat.MemoryManager.EndChatAsync();

        Console.WriteLine($"\n[IS COMPLETED: {chat.IsComplete}]");
    }

    private sealed class ApprovalTerminationStrategy : TerminationStrategy
    {
        // Terminate when the final message contains the term "approve"
        protected override Task<bool> ShouldAgentTerminateAsync(Agent agent, IReadOnlyList<ChatMessageContent> history, CancellationToken cancellationToken)
            => Task.FromResult(history[history.Count - 1].Content?.Contains("approve", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    [Fact]
    public async Task ChatWitSingleAgentAsync()
    {
        // Create a ChatHistory object to maintain the conversation state.
        ChatHistory chat = [];

        var userMessage = "My name is John. I live in Madrid. I like rain and the seaside.";

        var kernel = CreateKernelWithMemorySupport();

        ChatMemoryManager memoryManager = new(() => chat);
        memoryManager.RegisterMemoryComponent(new UserPreferencesMemory(kernel));
        await memoryManager.StartChatAsync($"Summarize user input. User Input: {userMessage}.");

        // Define the agent
        ChatCompletionAgent agent =
            new()
            {
                Instructions = "Summarize user input",
                Name = "SummarizationAgent",
                Kernel = kernel,
            };

        // Add a user message to the conversation
        var newMessage = new ChatMessageContent(AuthorRole.User, userMessage);
        chat.Add(newMessage);

        await memoryManager.MaintainContextAsync(newMessage);
        var memories = await memoryManager.GetRenderedContextAsync();

        // Generate the agent response(s)
        Console.WriteLine("# Agent response(s):");
        await foreach (ChatMessageContent response in agent.InvokeAsync(chat, overrideInstructions: memories))
        {
            Console.WriteLine(response.Content);
        }

        await memoryManager.EndChatAsync();
    }

    [Fact]
    public async Task ChatWitSingleAgentAndStorageAsync()
    {
        // Create a ChatHistory object to maintain the conversation state.
        ChatHistory chat = [];

        var userMessage = "My name is John. I live in Madrid. I like rain and the seaside.";

        var kernel = CreateKernelWithMemorySupport();

        ChatMemoryManager memoryManager = new(() => chat);
        memoryManager.RegisterMemoryComponent(new UserPreferencesMemory(kernel));
        await memoryManager.StartChatAsync($"Summarize user input. User Input: {userMessage}.");

        // Define the agent
        ChatCompletionAgent agent =
            new()
            {
                Instructions = "Summarize user input",
                Name = "SummarizationAgent",
                Kernel = kernel,
            };

        // Add a user message to the conversation
        var newMessage1 = new ChatMessageContent(AuthorRole.User, userMessage);
        chat.Add(newMessage1);

        await memoryManager.MaintainContextAsync(newMessage1);
        var memories = await memoryManager.GetRenderedContextAsync();

        // Generate the agent response(s)
        Console.WriteLine("# Agent response(s):");
        await foreach (ChatMessageContent response in agent.InvokeAsync(chat, overrideInstructions: memories))
        {
            Console.WriteLine(response.Content);
        }

        // Add another user message to the conversation
        var newMessage2 = new ChatMessageContent(AuthorRole.User, "I live in Paris");
        chat.Add(newMessage2);

        await memoryManager.MaintainContextAsync(newMessage2);
        memories = await memoryManager.GetRenderedContextAsync();

        // Generate the agent response(s)
        Console.WriteLine("# Agent response(s):");
        await foreach (ChatMessageContent response in agent.InvokeAsync(chat, overrideInstructions: memories))
        {
            Console.WriteLine(response.Content);
        }

        await memoryManager.EndChatAsync();

        // Second usage of memory manager should load previous context.
        ChatMemoryManager memoryManager2 = new(() => chat);
        memoryManager2.RegisterMemoryComponent(new UserPreferencesMemory(kernel));
        await memoryManager2.StartChatAsync(string.Empty);
        await memoryManager.EndChatAsync();
    }

    [Fact]
    public async Task UserPreferencesAsync()
    {
        var kernel = CreateKernelWithMemorySupport();

        // Create a ChatHistory object to maintain the conversation state.
        var userMessage = "My name is John. I live in Madrid. I like rain and the seaside.";

        ChatMemoryManager memoryManager = new(new ChatHistoryMemory());
        memoryManager.RegisterMemoryComponent(new UserPreferencesMemory(kernel));
        await memoryManager.StartChatAsync(userMessage);

        // Add a user message to the conversation
        var newMessage1 = new ChatMessageContent(AuthorRole.User, userMessage);

        await memoryManager.MaintainContextAsync(newMessage1);
        var memories = await memoryManager.GetRenderedContextAsync();

        // Add another user message to the conversation
        var newMessage2 = new ChatMessageContent(AuthorRole.User, "I live in Paris");

        await memoryManager.MaintainContextAsync(newMessage2);
        memories = await memoryManager.GetRenderedContextAsync();

        await memoryManager.EndChatAsync();

        // Second usage of memory manager should load previous context.
        ChatMemoryManager memoryManager2 = new(new ChatHistoryMemory());
        memoryManager2.RegisterMemoryComponent(new UserPreferencesMemory(kernel));
        await memoryManager2.StartChatAsync(string.Empty);
        await memoryManager.EndChatAsync();
    }

    protected Kernel CreateKernelWithMemorySupport()
    {
        var builder = Kernel.CreateBuilder();

        AddChatCompletionToKernel(builder);
        builder.AddAzureOpenAITextEmbeddingGeneration(
            TestConfiguration.AzureOpenAIEmbeddings.DeploymentName,
            TestConfiguration.AzureOpenAIEmbeddings.Endpoint,
            new AzureCliCredential());
        builder.Services.AddKeyedSingleton<MemoryDocumentStore>(
            "UserPreferencesStore",
            (sp, _) =>
            {
                return new VectorDataMemoryDocumentStore<string>(
                    new InMemoryVectorStore(),
                    sp.GetRequiredService<ITextEmbeddingGenerationService>(),
                    "UserPreferences",
                    "userid/12345",
                    1536);
            });

        return builder.Build();
    }
}
