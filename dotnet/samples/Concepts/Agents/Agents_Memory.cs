// Copyright (c) Microsoft. All rights reserved.

using System.ClientModel;
using Azure.Identity;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.AzureAI;
using Microsoft.SemanticKernel.Agents.Memory;
using Microsoft.SemanticKernel.Agents.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Data;
using OpenAI.Assistants;

namespace Agents;

public class Agents_Memory(ITestOutputHelper output) : BaseAgentsTest(output)
{
    [Fact]
    public async Task MinimalChatCompletionAgentWithMemZeroSampleAsync()
    {
        var kernel = CreateKernelWithMemorySupport();
        var httpClient = new HttpClient() { BaseAddress = new Uri("http://localhost:8000"), Timeout = TimeSpan.FromMinutes(10) };

        Console.WriteLine("------------ Session one --------------");
        var agentWithMemory = CreateAgent().WithMemory(memoryComponents: [new MemZeroMemoryComponent(httpClient, "FriendlyAssistant", "thread1", "User1")]);
        (await agentWithMemory.CompleteAsync(new ChatMessageContent(AuthorRole.User, "Hi, my name is Caoimhe")).ToListAsync()).ForEach(this.WriteAgentChatMessage);
        (await agentWithMemory.CompleteAsync(new ChatMessageContent(AuthorRole.User, "I love history, please tell me a historical fact")).ToListAsync()).ForEach(this.WriteAgentChatMessage);
        await agentWithMemory.EndThreadAsync();

        Console.WriteLine("------------ Session two --------------");
        var agentWithMemory2 = CreateAgent().WithMemory(memoryComponents: [new MemZeroMemoryComponent(httpClient, "FriendlyAssistant", "thread1", "User1")]);
        (await agentWithMemory2.CompleteAsync(new ChatMessageContent(AuthorRole.User, "What do you know about me?")).ToListAsync()).ForEach(this.WriteAgentChatMessage);
        (await agentWithMemory2.CompleteAsync(new ChatMessageContent(AuthorRole.User, "Please clear my user preferences.")).ToListAsync()).ForEach(this.WriteAgentChatMessage);
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
    public async Task MinimalChatCompletionAgentWitRAGSampleAsync()
    {
        var kernel = CreateKernelWithRAGSupport();

        var agentWithMemory = CreateAgent().WithMemory(memoryComponents: [new TextSearchRagMemoryComponent(kernel.GetRequiredService<VectorStoreTextSearch<RAGDataRecord>>(), null)]);
        (await agentWithMemory.CompleteAsync(new ChatMessageContent(AuthorRole.User, "How do I construct a Qdrant Vector store in C#")).ToListAsync()).ForEach(this.WriteAgentChatMessage);
        await agentWithMemory.EndThreadAsync();

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
    public async Task MinimalChatCompletionAgentWithMemorySampleAsync()
    {
        var kernel = CreateKernelWithMemorySupport();

        Console.WriteLine("------------ Session one --------------");
        var agentWithMemory = CreateAgent().WithMemory(memoryComponents: [new ChatHistorySummarizedStorageMemoryComponent(kernel), new UserPreferencesMemoryComponent(kernel)]);
        (await agentWithMemory.CompleteAsync(new ChatMessageContent(AuthorRole.User, "Hi, my name is Caoimhe")).ToListAsync()).ForEach(this.WriteAgentChatMessage);
        (await agentWithMemory.CompleteAsync(new ChatMessageContent(AuthorRole.User, "I love history, please tell me a historical fact")).ToListAsync()).ForEach(this.WriteAgentChatMessage);
        await agentWithMemory.EndThreadAsync();

        Console.WriteLine("------------ Session two --------------");
        var agentWithMemory2 = CreateAgent().WithMemory(memoryComponents: [new ChatHistorySummarizedStorageMemoryComponent(kernel), new UserPreferencesMemoryComponent(kernel)]);
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
            var agentWithMemory = (await CreateAgent()).WithMemory(memoryComponents: [new ChatHistorySummarizedStorageMemoryComponent(kernel), new UserPreferencesMemoryComponent(kernel)]);
            (await agentWithMemory.CompleteAsync(new ChatMessageContent(AuthorRole.User, "Hi, my name is Caoimhe")).ToListAsync()).ForEach(this.WriteAgentChatMessage);
            (await agentWithMemory.CompleteAsync(new ChatMessageContent(AuthorRole.User, "I love history, please tell me a historical fact")).ToListAsync()).ForEach(this.WriteAgentChatMessage);
            await agentWithMemory.EndThreadAsync();

            Console.WriteLine("------------ Session two --------------");
            var agentWithMemory2 = (await CreateAgent()).WithMemory(memoryComponents: [new ChatHistorySummarizedStorageMemoryComponent(kernel), new UserPreferencesMemoryComponent(kernel)]);
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
            AgentWithMemory agentWithMemory = agent.WithMemory([new ChatHistorySummarizedStorageMemoryComponent(kernel), new UserPreferencesMemoryComponent(kernel)]);

            (await agentWithMemory.CompleteAsync(new ChatMessageContent(AuthorRole.User, "Please consolidate today's invoices and payments.")).ToListAsync()).ForEach(this.WriteAgentChatMessage);
            (await agentWithMemory.CompleteAsync(new ChatMessageContent(AuthorRole.User, "I am working with Contoso and I always want format B.")).ToListAsync()).ForEach(this.WriteAgentChatMessage);

            await agentWithMemory.EndThreadAsync();

            Console.WriteLine("------------ Session two --------------");

            // Second usage of memory manager should load previous context.
            agentWithMemory = agent.WithMemory([new ChatHistorySummarizedStorageMemoryComponent(kernel), new UserPreferencesMemoryComponent(kernel)]);

            (await agentWithMemory.CompleteAsync(new ChatMessageContent(AuthorRole.User, "Please consolidate today's invoices and payments.")).ToListAsync()).ForEach(this.WriteAgentChatMessage);

            await agentWithMemory.EndThreadAsync();

            Console.WriteLine("------------ Session three --------------");

            // Third usage of memory manager should load previous context.
            agentWithMemory = agent.WithMemory([new ChatHistorySummarizedStorageMemoryComponent(kernel), new UserPreferencesMemoryComponent(kernel)]);

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
        AgentWithMemory agentWithMemory = agent.WithMemory([new ChatHistorySummarizedStorageMemoryComponent(kernel), new UserPreferencesMemoryComponent(kernel)]);

        (await agentWithMemory.CompleteAsync(new ChatMessageContent(AuthorRole.User, "Please consolidate today's invoices and payments.")).ToListAsync()).ForEach(this.WriteAgentChatMessage);
        (await agentWithMemory.CompleteAsync(new ChatMessageContent(AuthorRole.User, "I am working with Contoso and I always want format B.")).ToListAsync()).ForEach(this.WriteAgentChatMessage);

        await agentWithMemory.EndThreadAsync();

        Console.WriteLine("------------ Session two --------------");

        // Second usage of memory manager should load previous context.
        agentWithMemory = agent.WithMemory([new ChatHistorySummarizedStorageMemoryComponent(kernel), new UserPreferencesMemoryComponent(kernel)]);

        (await agentWithMemory.CompleteAsync(new ChatMessageContent(AuthorRole.User, "Please consolidate today's invoices and payments.")).ToListAsync()).ForEach(this.WriteAgentChatMessage);

        await agentWithMemory.EndThreadAsync();

        Console.WriteLine("------------ Session three --------------");

        // Third usage of memory manager should load previous context.
        agentWithMemory = agent.WithMemory([new ChatHistorySummarizedStorageMemoryComponent(kernel), new UserPreferencesMemoryComponent(kernel)]);

        (await agentWithMemory.CompleteAsync(new ChatMessageContent(AuthorRole.User, "What do you know about me?")).ToListAsync()).ForEach(this.WriteAgentChatMessage);
        (await agentWithMemory.CompleteAsync(new ChatMessageContent(AuthorRole.User, "Please clear my user preferences.")).ToListAsync()).ForEach(this.WriteAgentChatMessage);

        await agentWithMemory.EndThreadAsync();
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

    protected Kernel CreateKernelWithRAGSupport()
    {
        var builder = Kernel.CreateBuilder();

        AddChatCompletionToKernel(builder);
        builder.AddAzureOpenAITextEmbeddingGeneration(
            TestConfiguration.AzureOpenAIEmbeddings.DeploymentName,
            TestConfiguration.AzureOpenAIEmbeddings.Endpoint,
            new AzureCliCredential());
        builder.Services.AddQdrantVectorStoreRecordCollection<Guid, RAGDataRecord>("pdfcontent", "localhost");
        builder.Services.AddVectorStoreTextSearch<RAGDataRecord>();

        return builder.Build();
    }

    public class RAGDataRecord
    {
        [VectorStoreRecordKey]
        public required Guid Key { get; set; }

        [VectorStoreRecordData]
        [TextSearchResultValue]
        public string? Text { get; set; }

        [VectorStoreRecordData]
        [TextSearchResultName]
        public string? ReferenceDescription { get; set; }

        [VectorStoreRecordData]
        [TextSearchResultLink]
        public string? ReferenceLink { get; set; }

        [VectorStoreRecordVector(1536)]
        public ReadOnlyMemory<float> TextEmbedding { get; set; }
    }
}
