// Copyright (c) Microsoft. All rights reserved.
using System.ClientModel;
using Azure.Identity;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.AzureAI;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.Agents.Memory;
using Microsoft.SemanticKernel.Agents.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI.Assistants;

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

        chat.MemoryManager.RegisterMemoryComponent(new UserPreferencesMemoryComponent(kernel));
        await chat.MemoryManager.LoadContextAsync("concept: maps made out of egg cartons.");

        // Invoke chat and display messages.
        ChatMessageContent input = new(AuthorRole.User, "concept: maps made out of egg cartons.");
        chat.AddChatMessage(input);
        this.WriteAgentChatMessage(input);

        await foreach (ChatMessageContent response in chat.InvokeAsync())
        {
            this.WriteAgentChatMessage(response);
        }

        await chat.MemoryManager.SaveContextAsync();

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

        var userMessage = "My name is Eoin. I live in Madrid. I like rain and the seaside.";

        var kernel = CreateKernelWithMemorySupport();

        ChatHistoryMemoryManager memoryManager = new(() => chat);
        memoryManager.RegisterMemoryComponent(new UserPreferencesMemoryComponent(kernel));
        await memoryManager.LoadContextAsync($"Summarize user input. User Input: {userMessage}.");

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
        var memories = await memoryManager.GetFormattedContextAsync();

        // Generate the agent response(s)
        Console.WriteLine("# Agent response(s):");
        await foreach (ChatMessageContent response in agent.InvokeAsync(chat, overrideInstructions: memories))
        {
            Console.WriteLine(response.Content);
        }

        await memoryManager.SaveContextAsync();
    }

    [Fact]
    public async Task ChatWitSingleAgentAndStorageAsync()
    {
        // Create a ChatHistory object to maintain the conversation state.
        ChatHistory chat = [];

        var userMessage = "My name is Eoin. I live in Madrid. I like rain and the seaside.";

        var kernel = CreateKernelWithMemorySupport();

        ChatHistoryMemoryManager memoryManager = new(() => chat);
        memoryManager.RegisterMemoryComponent(new UserPreferencesMemoryComponent(kernel));
        await memoryManager.LoadContextAsync($"Summarize user input. User Input: {userMessage}.");

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
        var memories = await memoryManager.GetFormattedContextAsync();

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
        memories = await memoryManager.GetFormattedContextAsync();

        // Generate the agent response(s)
        Console.WriteLine("# Agent response(s):");
        await foreach (ChatMessageContent response in agent.InvokeAsync(chat, overrideInstructions: memories))
        {
            Console.WriteLine(response.Content);
        }

        await memoryManager.SaveContextAsync();

        // Second usage of memory manager should load previous context.
        ChatHistoryMemoryManager memoryManager2 = new(() => chat);
        memoryManager2.RegisterMemoryComponent(new UserPreferencesMemoryComponent(kernel));
        await memoryManager2.LoadContextAsync(string.Empty);
        await memoryManager.SaveContextAsync();
    }

    [Fact]
    public async Task UserPreferencesAsync()
    {
        Console.WriteLine("------------ Session one --------------");

        var kernel = CreateKernelWithMemorySupport();

        // Create memory manager and register memory components.
        ChatHistoryMemoryManager memoryManager = new(new ChatHistoryMemoryComponent(kernel));
        memoryManager.RegisterMemoryComponent(new UserPreferencesMemoryComponent(kernel));

        await memoryManager.MaintainContextAsync(new ChatMessageContent(AuthorRole.Assistant, "How can I help you?") { Source = "MyAgent" });

        var userMessage = "My name is Eoin. I live in Madrid. I like rain and the seaside.";
        await memoryManager.LoadContextAsync(userMessage);
        await memoryManager.MaintainContextAsync(new ChatMessageContent(AuthorRole.User, userMessage));
        await memoryManager.MaintainContextAsync(new ChatMessageContent(AuthorRole.User, "This chat is very dreary."));

        await memoryManager.SaveContextAsync();

        Console.WriteLine("------------ Session two --------------");

        // Second usage of memory manager should load previous context.
        ChatHistoryMemoryManager memoryManager2 = new(new ChatHistoryMemoryComponent(kernel));
        memoryManager2.RegisterMemoryComponent(new UserPreferencesMemoryComponent(kernel));
        await memoryManager2.LoadContextAsync("Hi there");

        // Add a user message to the conversation
        await memoryManager2.MaintainContextAsync(new ChatMessageContent(AuthorRole.User, "I now live in Paris."));

        await memoryManager2.SaveContextAsync();
    }

    [Fact]
    public async Task MinimalChatCompletionAgentWithMemorySampleAsync()
    {
        var kernel = CreateKernelWithMemorySupport();

        Console.WriteLine("------------ Session one --------------");
        var agentWithMemory = CreateAgent().WithMemory(memoryComponents: [new UserPreferencesMemoryComponent(kernel)]);
        (await agentWithMemory.CompleteAsync(new ChatMessageContent(AuthorRole.User, "Hi, my name is Caoimhe")).ToListAsync()).ForEach(this.WriteAgentChatMessage);
        (await agentWithMemory.CompleteAsync(new ChatMessageContent(AuthorRole.User, "I love history, please tell me a historical fact")).ToListAsync()).ForEach(this.WriteAgentChatMessage);
        await agentWithMemory.EndThreadAsync();

        Console.WriteLine("------------ Session two --------------");
        var agentWithMemory2 = CreateAgent().WithMemory(memoryComponents: [new UserPreferencesMemoryComponent(kernel)]);
        (await agentWithMemory2.CompleteAsync(new ChatMessageContent(AuthorRole.User, "What do you know about me?")).ToListAsync()).ForEach(this.WriteAgentChatMessage);
        await agentWithMemory2.EndThreadAsync();

        ChatCompletionAgent CreateAgent()
        {
            return new()
            {
                Instructions = "You are a friendly assistant",
                Name = "FriendlyAssistant",
                Kernel = kernel,
            };
        }
    }

    [Fact]
    public async Task MinimalAzureAIAgentWithMemorySampleAsync()
    {
        var kernel = CreateKernelWithMemorySupport();

        var azureAIClient = AzureAIAgent.CreateAzureAIClient(TestConfiguration.AzureAI.ConnectionString, new AzureCliCredential());
        var azureAIAgentsClient = azureAIClient.GetAgentsClient();
        var createAgentResponse = await azureAIAgentsClient.CreateAgentAsync("gpt-4o", "FriendlyAssistant", "FriendlyAssistant", "You are a friendly assistant");

        try
        {
            Console.WriteLine("------------ Session one --------------");
            var agentWithMemory = (await CreateAgent()).WithMemory(memoryComponents: [new UserPreferencesMemoryComponent(kernel)]);
            (await agentWithMemory.CompleteAsync(new ChatMessageContent(AuthorRole.User, "Hi, my name is Caoimhe")).ToListAsync()).ForEach(this.WriteAgentChatMessage);
            (await agentWithMemory.CompleteAsync(new ChatMessageContent(AuthorRole.User, "I love history, please tell me a historical fact")).ToListAsync()).ForEach(this.WriteAgentChatMessage);
            await agentWithMemory.EndThreadAsync();

            Console.WriteLine("------------ Session two --------------");
            var agentWithMemory2 = (await CreateAgent()).WithMemory(memoryComponents: [new UserPreferencesMemoryComponent(kernel)]);
            (await agentWithMemory2.CompleteAsync(new ChatMessageContent(AuthorRole.User, "What do you know about me?")).ToListAsync()).ForEach(this.WriteAgentChatMessage);
            await agentWithMemory2.EndThreadAsync();
        }
        finally
        {
            await azureAIAgentsClient.DeleteAgentAsync(createAgentResponse.Value.Id);
        }

        async Task<AzureAIAgent> CreateAgent()
        {
            return new(createAgentResponse.Value, azureAIAgentsClient) { Kernel = kernel };
        }
    }

    [Fact]
    public async Task FinancialReportWithMemoryAssistantAgentAsync()
    {
        var kernel = CreateKernelWithMemorySupport();

        // Connect to OpenAI assistants.
        var client =
            this.UseOpenAIConfig ?
                OpenAIAssistantAgent.CreateOpenAIClient(new ApiKeyCredential(this.ApiKey ?? throw new ConfigurationNotFoundException("OpenAI:ApiKey"))) :
                !string.IsNullOrWhiteSpace(this.ApiKey) ?
                    OpenAIAssistantAgent.CreateAzureOpenAIClient(new ApiKeyCredential(this.ApiKey), new Uri(this.Endpoint!)) :
                    OpenAIAssistantAgent.CreateAzureOpenAIClient(new AzureCliCredential(), new Uri(this.Endpoint!));

        var assistantClient = client.GetAssistantClient();

        // Define the assistant
        Assistant assistant =
            await assistantClient.CreateAssistantAsync(
                this.Model,
                name: "FinanceAgent",
                instructions: "You are an expert in financial management and accounting and can use tools to consolidate invoices and payments");

        // Create the agent
        var agentKernel = kernel.Clone();
        agentKernel.Plugins.AddFromType<FinancialPlugin>();
        OpenAIAssistantAgent agent = new(assistant, assistantClient) { Kernel = agentKernel };

        try
        {
            Console.WriteLine("------------ Session one --------------");

            // Create agent with memory and register memory components.
            AgentWithMemory agentWithMemory = agent.WithMemory([new UserPreferencesMemoryComponent(kernel)]);

            (await agentWithMemory.CompleteAsync(new ChatMessageContent(AuthorRole.User, "Please consolidate today's invoices and payments.")).ToListAsync()).ForEach(this.WriteAgentChatMessage);
            (await agentWithMemory.CompleteAsync(new ChatMessageContent(AuthorRole.User, "I am working with Contoso and I always want format B.")).ToListAsync()).ForEach(this.WriteAgentChatMessage);

            await agentWithMemory.EndThreadAsync();

            Console.WriteLine("------------ Session two --------------");

            // Second usage of memory manager should load previous context.
            agentWithMemory = agent.WithMemory([new UserPreferencesMemoryComponent(kernel)]);

            (await agentWithMemory.CompleteAsync(new ChatMessageContent(AuthorRole.User, "Please consolidate today's invoices and payments.")).ToListAsync()).ForEach(this.WriteAgentChatMessage);

            await agentWithMemory.EndThreadAsync();

            Console.WriteLine("------------ Session three --------------");

            // Third usage of memory manager should load previous context.
            agentWithMemory = agent.WithMemory([new UserPreferencesMemoryComponent(kernel)]);

            (await agentWithMemory.CompleteAsync(new ChatMessageContent(AuthorRole.User, "What do you know about me?")).ToListAsync()).ForEach(this.WriteAgentChatMessage);
            (await agentWithMemory.CompleteAsync(new ChatMessageContent(AuthorRole.User, "Please clear my user preferences.")).ToListAsync()).ForEach(this.WriteAgentChatMessage);

            await agentWithMemory.EndThreadAsync();
        }
        finally
        {
            await assistantClient.DeleteAssistantAsync(agent.Id);
        }
    }

    [Fact]
    public async Task FinancialReportWithChatCompletionAgentAsync()
    {
        var kernel = CreateKernelWithMemorySupport();

        // Define the agent
        var agentKernel = kernel.Clone();
        agentKernel.Plugins.AddFromType<FinancialPlugin>();
        ChatCompletionAgent agent =
            new()
            {
                Instructions = "You are an expert in financial management and accounting and can use tools to consolidate invoices and payments",
                Name = "FinanceAgent",
                Kernel = agentKernel,
            };

        Console.WriteLine("------------ Session one --------------");

        // TODO: Suspend / Resume.

        // Create agent with memory and register memory components.
        AgentWithMemory agentWithMemory = agent.WithMemory(kernel, [new UserPreferencesMemoryComponent(kernel)]);

        (await agentWithMemory.CompleteAsync(new ChatMessageContent(AuthorRole.User, "Please consolidate today's invoices and payments.")).ToListAsync()).ForEach(this.WriteAgentChatMessage);
        (await agentWithMemory.CompleteAsync(new ChatMessageContent(AuthorRole.User, "I am working with Contoso and I always want format B.")).ToListAsync()).ForEach(this.WriteAgentChatMessage);

        await agentWithMemory.EndThreadAsync();

        Console.WriteLine("------------ Session two --------------");

        // Second usage of memory manager should load previous context.
        agentWithMemory = agent.WithMemory(kernel, [new UserPreferencesMemoryComponent(kernel)]);

        (await agentWithMemory.CompleteAsync(new ChatMessageContent(AuthorRole.User, "Please consolidate today's invoices and payments.")).ToListAsync()).ForEach(this.WriteAgentChatMessage);

        await agentWithMemory.EndThreadAsync();

        Console.WriteLine("------------ Session three --------------");

        // Third usage of memory manager should load previous context.
        agentWithMemory = agent.WithMemory(kernel, [new UserPreferencesMemoryComponent(kernel)]);

        (await agentWithMemory.CompleteAsync(new ChatMessageContent(AuthorRole.User, "What do you know about me?")).ToListAsync()).ForEach(this.WriteAgentChatMessage);
        (await agentWithMemory.CompleteAsync(new ChatMessageContent(AuthorRole.User, "Please clear my user preferences.")).ToListAsync()).ForEach(this.WriteAgentChatMessage);

        await agentWithMemory.EndThreadAsync();
    }

    [Fact]
    public async Task FinancialReportAsync()
    {
        var kernel = CreateKernelWithMemorySupport();

        // Define the agent
        var agentKernel = kernel.Clone();
        agentKernel.Plugins.AddFromType<FinancialPlugin>();
        ChatCompletionAgent agent =
            new()
            {
                Instructions = "You are an expert in financial management and accounting and can use tools to consolidate invoices and payments",
                Name = "FinanceAgent",
                Kernel = agentKernel,
            };

        Console.WriteLine("------------ Session one --------------");

        // Create memory manager and register memory components.
        ChatHistoryMemoryManager memoryManager1 = new(new ChatHistoryMemoryComponent(kernel));
        memoryManager1.RegisterMemoryComponent(new UserPreferencesMemoryComponent(kernel));

        var userMessage = "Please consolidate today's invoices and payments.";
        await memoryManager1.LoadContextAsync(userMessage);
        await this.InvokeAgentAsync(agent, memoryManager1, userMessage);
        await this.InvokeAgentAsync(agent, memoryManager1, "I am working with Contoso and I always want format B.");

        await memoryManager1.SaveContextAsync();

        Console.WriteLine("------------ Session two --------------");

        // Second usage of memory manager should load previous context.
        ChatHistoryMemoryManager memoryManager2 = new(new ChatHistoryMemoryComponent(kernel));
        memoryManager2.RegisterMemoryComponent(new UserPreferencesMemoryComponent(kernel));

        var userMessage2 = "Please consolidate today's invoices and payments.";
        await memoryManager2.LoadContextAsync(userMessage2);
        await this.InvokeAgentAsync(agent, memoryManager2, userMessage);

        await memoryManager2.SaveContextAsync();

        Console.WriteLine("------------ Session three --------------");

        // Third usage of memory manager should load previous context.
        ChatHistoryMemoryManager memoryManager3 = new(new ChatHistoryMemoryComponent(kernel));
        memoryManager3.RegisterMemoryComponent(new UserPreferencesMemoryComponent(kernel));

        var userMessage3 = "What do you know about me?";
        await memoryManager3.LoadContextAsync(userMessage3);
        await this.InvokeAgentAsync(agent, memoryManager3, userMessage3);

        await this.InvokeAgentAsync(agent, memoryManager3, "Please clear my user preferences.");

        await memoryManager3.SaveContextAsync();
    }

    private async Task InvokeAgentAsync(ChatCompletionAgent agent, ChatHistoryMemoryManager memoryManager, string userMessage)
    {
        await memoryManager.MaintainContextAsync(new ChatMessageContent(AuthorRole.User, userMessage));
        var memoryContext = await memoryManager.GetFormattedContextAsync();

        var overrideKernel = agent.Kernel.Clone();
        memoryManager.RegisterPlugins(overrideKernel);

        // Generate the agent response(s)
        await foreach (ChatMessageContent response in agent.InvokeAsync(
            memoryManager.ChatHistory,
            new KernelArguments(new PromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() }),
            overrideInstructions: memoryContext,
            overrideKernel))
        {
            Console.WriteLine($"# {agent.Name} Agent response(s):");
            Console.WriteLine($"    {response.Content}");

            if (response.Role == AuthorRole.Assistant)
            {
                await memoryManager.MaintainContextAsync(response);
            }
        }
    }

    private class FinancialPlugin
    {
        [KernelFunction]
        public async Task<string> ConsolidateInvoicesAndPaymentsAsync(string company, ReportFormat outputFormat, CancellationToken cancellationToken)
        {
            if (company != "Contoso")
            {
                throw new ArgumentException("Unknwon Company");
            }

            return "Here is your report: 123, 456, 789, ...";
        }
    }

    private enum ReportFormat
    {
        A,
        B,
        C
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
