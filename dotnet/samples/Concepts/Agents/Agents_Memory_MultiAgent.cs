// Copyright (c) Microsoft. All rights reserved.

using Azure.Identity;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.Agents.Memory;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Agents;

public class Agents_Memory_MultiAgent(ITestOutputHelper output) : BaseAgentsTest(output)
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

        chat.MemoryManager.RegisterMemoryComponent(new UserPreferencesMemoryComponent(kernel));
        await chat.MemoryManager.OnThreadStartAsync("t1", "concept: maps made out of egg cartons.");

        // Invoke chat and display messages.
        ChatMessageContent input = new(AuthorRole.User, "concept: maps made out of egg cartons.");
        chat.AddChatMessage(input);
        this.WriteAgentChatMessage(input);

        await foreach (ChatMessageContent response in chat.InvokeAsync())
        {
            this.WriteAgentChatMessage(response);
        }

        await chat.MemoryManager.OnThreadEndAsync("t1");

        Console.WriteLine($"\n[IS COMPLETED: {chat.IsComplete}]");
    }

    private sealed class ApprovalTerminationStrategy : TerminationStrategy
    {
        // Terminate when the final message contains the term "approve"
        protected override Task<bool> ShouldAgentTerminateAsync(Agent agent, IReadOnlyList<ChatMessageContent> history, CancellationToken cancellationToken)
            => Task.FromResult(history[history.Count - 1].Content?.Contains("approve", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    protected Kernel CreateKernelWithMemorySupport()
    {
        var builder = Kernel.CreateBuilder();

        AddChatCompletionToKernel(builder);
        builder.AddAzureOpenAITextEmbeddingGeneration(
            TestConfiguration.AzureOpenAIEmbeddings.DeploymentName,
            TestConfiguration.AzureOpenAIEmbeddings.Endpoint,
            new AzureCliCredential());
        builder.Services.AddInMemoryVectorStore();
        builder.Services.AddTransientUserPreferencesMemoryDocumentStore(
            1536,
            "userid/12345");
        builder.Services.AddTransientChatHistoryMemoryDocumentStore(
            1536,
            "userid/12345");

        return builder.Build();
    }
}
