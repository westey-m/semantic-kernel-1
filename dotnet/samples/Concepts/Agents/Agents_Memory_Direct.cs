// Copyright (c) Microsoft. All rights reserved.

using Azure.Identity;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Memory;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Agents;

public class Agents_Memory_Direct(ITestOutputHelper output) : BaseAgentsTest(output)
{
    [Fact]
    public async Task ChatWitSingleAgentAsync()
    {
        // Create a ChatHistory object to maintain the conversation state.
        ChatHistory chat = [];

        var userMessage = "My name is Eoin. I live in Madrid. I like rain and the seaside.";

        var kernel = CreateKernelWithMemorySupport();

        ChatHistoryMemoryManager memoryManager = new(() => chat);
        memoryManager.RegisterMemoryComponent(new UserPreferencesMemoryComponent(kernel));
        await memoryManager.OnThreadStartAsync("t1", $"Summarize user input. User Input: {userMessage}.");

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

        await memoryManager.OnNewMessageAsync(newMessage);
        var memories = await memoryManager.OnAIInvocationAsync(newMessage);

        // Generate the agent response(s)
        Console.WriteLine("# Agent response(s):");
        await foreach (ChatMessageContent response in agent.InvokeAsync(chat, overrideInstructions: memories))
        {
            Console.WriteLine(response.Content);
        }

        await memoryManager.OnThreadEndAsync("t1");
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
        await memoryManager.OnThreadStartAsync("t1", $"Summarize user input. User Input: {userMessage}.");

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

        await memoryManager.OnNewMessageAsync(newMessage1);
        var memories = await memoryManager.OnAIInvocationAsync(newMessage1);

        // Generate the agent response(s)
        Console.WriteLine("# Agent response(s):");
        await foreach (ChatMessageContent response in agent.InvokeAsync(chat, overrideInstructions: memories))
        {
            Console.WriteLine(response.Content);
        }

        // Add another user message to the conversation
        var newMessage2 = new ChatMessageContent(AuthorRole.User, "I live in Paris");
        chat.Add(newMessage2);

        await memoryManager.OnNewMessageAsync(newMessage2);
        memories = await memoryManager.OnAIInvocationAsync(newMessage2);

        // Generate the agent response(s)
        Console.WriteLine("# Agent response(s):");
        await foreach (ChatMessageContent response in agent.InvokeAsync(chat, overrideInstructions: memories))
        {
            Console.WriteLine(response.Content);
        }

        await memoryManager.OnThreadEndAsync("t1");

        // Second usage of memory manager should load previous context.
        ChatHistoryMemoryManager memoryManager2 = new(() => chat);
        memoryManager2.RegisterMemoryComponent(new UserPreferencesMemoryComponent(kernel));
        await memoryManager2.OnThreadStartAsync("t1", string.Empty);
        await memoryManager.OnThreadEndAsync("t1");
    }

    [Fact]
    public async Task UserPreferencesAsync()
    {
        Console.WriteLine("------------ Session one --------------");

        var kernel = CreateKernelWithMemorySupport();

        // Create memory manager and register memory components.
        ChatHistoryMemoryManager memoryManager = new(new ChatHistoryMemoryComponent(kernel));
        memoryManager.RegisterMemoryComponent(new UserPreferencesMemoryComponent(kernel));

        await memoryManager.OnNewMessageAsync(new ChatMessageContent(AuthorRole.Assistant, "How can I help you?") { Source = "MyAgent" });

        var userMessage = "My name is Eoin. I live in Madrid. I like rain and the seaside.";
        await memoryManager.OnThreadStartAsync("t1", userMessage);
        await memoryManager.OnNewMessageAsync(new ChatMessageContent(AuthorRole.User, userMessage));
        await memoryManager.OnNewMessageAsync(new ChatMessageContent(AuthorRole.User, "This chat is very dreary."));

        await memoryManager.OnThreadEndAsync("t1");

        Console.WriteLine("------------ Session two --------------");

        // Second usage of memory manager should load previous context.
        ChatHistoryMemoryManager memoryManager2 = new(new ChatHistoryMemoryComponent(kernel));
        memoryManager2.RegisterMemoryComponent(new UserPreferencesMemoryComponent(kernel));
        await memoryManager2.OnThreadStartAsync("t1", "Hi there");

        // Add a user message to the conversation
        await memoryManager2.OnNewMessageAsync(new ChatMessageContent(AuthorRole.User, "I now live in Paris."));

        await memoryManager2.OnThreadEndAsync("t1");
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
        await memoryManager1.OnThreadStartAsync("t1", userMessage);
        await this.InvokeAgentAsync(agent, memoryManager1, userMessage);
        await this.InvokeAgentAsync(agent, memoryManager1, "I am working with Contoso and I always want format B.");

        await memoryManager1.OnThreadEndAsync("t1");

        Console.WriteLine("------------ Session two --------------");

        // Second usage of memory manager should load previous context.
        ChatHistoryMemoryManager memoryManager2 = new(new ChatHistoryMemoryComponent(kernel));
        memoryManager2.RegisterMemoryComponent(new UserPreferencesMemoryComponent(kernel));

        var userMessage2 = "Please consolidate today's invoices and payments.";
        await memoryManager2.OnThreadStartAsync("t1", userMessage2);
        await this.InvokeAgentAsync(agent, memoryManager2, userMessage);

        await memoryManager2.OnThreadEndAsync("t1");

        Console.WriteLine("------------ Session three --------------");

        // Third usage of memory manager should load previous context.
        ChatHistoryMemoryManager memoryManager3 = new(new ChatHistoryMemoryComponent(kernel));
        memoryManager3.RegisterMemoryComponent(new UserPreferencesMemoryComponent(kernel));

        var userMessage3 = "What do you know about me?";
        await memoryManager3.OnThreadStartAsync("t1", userMessage3);
        await this.InvokeAgentAsync(agent, memoryManager3, userMessage3);

        await this.InvokeAgentAsync(agent, memoryManager3, "Please clear my user preferences.");

        await memoryManager3.OnThreadEndAsync("t1");
    }

    private async Task InvokeAgentAsync(ChatCompletionAgent agent, ChatHistoryMemoryManager memoryManager, string userMessage)
    {
        var message = new ChatMessageContent(AuthorRole.User, userMessage);
        await memoryManager.OnNewMessageAsync(message);
        var memoryContext = await memoryManager.OnAIInvocationAsync(message);

        var overrideKernel = agent.Kernel.Clone();
        memoryManager.RegisterPlugins(overrideKernel);

        // Generate the agent response(s)
        var chatHistory = await memoryManager.RetrieveCurrentChatHistoryAsync();
        await foreach (ChatMessageContent response in agent.InvokeAsync(
            chatHistory,
            new KernelArguments(new PromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() }),
            overrideInstructions: memoryContext,
            overrideKernel))
        {
            Console.WriteLine($"# {agent.Name} Agent response(s):");
            Console.WriteLine($"    {response.Content}");

            if (response.Role == AuthorRole.Assistant)
            {
                await memoryManager.OnNewMessageAsync(response);
            }
        }
    }

    private sealed class FinancialPlugin
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
