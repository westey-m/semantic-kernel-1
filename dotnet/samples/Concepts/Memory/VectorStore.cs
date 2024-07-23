// Copyright (c) Microsoft. All rights reserved.

using System.Runtime.InteropServices;
using Docker.DotNet;
using Memory.VectorStoreFixtures;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel.Connectors.Redis;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Embeddings;
using MongoDB.Driver;
using NRedisStack.RedisStackCommands;
using NRedisStack.Search;
using Qdrant.Client;
using StackExchange.Redis;

namespace Memory;

#pragma warning disable CA1859 // Use concrete types when possible for improved performance

/// <summary>
/// Qdrant: http://localhost:6333/dashboard
/// Redis: http://localhost:8001/redis-stack/browser
/// </summary>
public class VectorStore(ITestOutputHelper output) : BaseTest(output)
{
    private sealed class Hotel<TKey>
    {
        [VectorStoreRecordKey]
        public TKey HotelId { get; init; }

        [VectorStoreRecordData(IsFilterable = true)]
        public string HotelName { get; init; }

        [VectorStoreRecordData(IsFilterable = true)]
        public float HotelRating { get; init; }

        [VectorStoreRecordData(IsFilterable = true)]
        public bool ParkingIncluded { get; init; }

        [VectorStoreRecordData(HasEmbedding = true, EmbeddingPropertyName = "DescriptionEmbedding")]
        public string Description { get; init; }

        [VectorStoreRecordVector(1536)]
        public ReadOnlyMemory<float>? DescriptionEmbedding { get; init; }
    }

    [Fact]
    public async Task RunCreateExampleAsync()
    {
        // Create docker containers.
        using var dockerClientConfiguration = new DockerClientConfiguration();
        var client = dockerClientConfiguration.CreateClient();
        var qdrantContainerId = await VectorStoreInfra.SetupQdrantContainerAsync(client);
        var redisContainerId = await VectorStoreInfra.SetupRedisContainerAsync(client);

        // Create the qdrant vector store clients.
        var qdrantClient = new QdrantClient("localhost");
        var qdrantVectorStore = new QdrantVectorStore(qdrantClient);
        var qdrantCollection = qdrantVectorStore.GetCollection<ulong, Hotel<ulong>>("hotels");

        // Create the redis vector store clients.
        ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("localhost:6379");
        var redisDatabase = redis.GetDatabase();
        var redisVectorStore = new RedisVectorStore(redisDatabase);
        var redisCollection = redisVectorStore.GetCollection<string, Hotel<string>>("hotels");

        // Create Embedding Service
        var embeddingService = new AzureOpenAITextEmbeddingGenerationService(
                TestConfiguration.AzureOpenAIEmbeddings.DeploymentName,
                TestConfiguration.AzureOpenAIEmbeddings.Endpoint,
                TestConfiguration.AzureOpenAIEmbeddings.ApiKey);

        // Create collection and add data.
        await this.CreateCollectionAndAddDataAsync(redisCollection, embeddingService, "testRecord");
        await this.CreateCollectionAndAddDataAsync(qdrantCollection, embeddingService, 5ul);

        // Search.
        var queryEmbedding = await embeddingService.GenerateEmbeddingAsync("A magical fusion of hotel and beach.");

        byte[] byteArray = new byte[queryEmbedding.Length * sizeof(float)];
        Buffer.BlockCopy(queryEmbedding.ToArray(), 0, byteArray, 0, byteArray.Length);
        string hex = BitConverter.ToString(byteArray).Replace("-", "x");

        var query = new NRedisStack.Search.Query("*=>[KNN 3 @DescriptionEmbedding $vector AS score]")
            .AddParam("vector", MemoryMarshal.AsBytes(queryEmbedding.Span).ToArray())
            //.AddParam("vector", byteArray)
            .ReturnFields("$.HotelName", "score")
            .Limit(0, 1)
            .Dialect(2);
        var searchResult = await redisDatabase.FT().SearchAsync("hotels", query);

        var query2 = new NRedisStack.Search.Query("*")
            .ReturnFields(new FieldName("$.HotelName", "HotelName"))
            .Limit(0, 1)
            .Dialect(2);
        var searchResult2 = await redisDatabase.FT().SearchAsync("hotels", query2);

        // Delete record and collection.
        await this.DeleteRecordAndCollectionAsync(redisCollection, embeddingService, "testRecord");
        await this.DeleteRecordAndCollectionAsync(qdrantCollection, embeddingService, 5ul);

        // Delete docker containers.
        await VectorStoreInfra.DeleteContainerAsync(client, qdrantContainerId);
        await VectorStoreInfra.DeleteContainerAsync(client, redisContainerId);
    }

    private async Task CreateCollectionAndAddDataAsync<TKey>(
        IVectorStoreRecordCollection<TKey, Hotel<TKey>> vectorRecordStore,
        ITextEmbeddingGenerationService embeddingGenerationService,
        TKey recordKey)
    {
        // Create collection.
        await vectorRecordStore.CreateCollectionAsync();

        // Generate Embeddings.
        var description = "A magical fusion of hotel and beach.";
        var embedding = await embeddingGenerationService.GenerateEmbeddingAsync(description);

        // Upsert Record.
        await vectorRecordStore.UpsertAsync(
            new Hotel<TKey>
            {
                HotelId = recordKey,
                HotelName = "Paradise Patch",
                HotelRating = 4.5f,
                ParkingIncluded = true,
                Description = description,
                DescriptionEmbedding = embedding
            });

        // Retrieve Record.
        var record = await vectorRecordStore.GetAsync(recordKey, new() { IncludeVectors = true });
    }

    private async Task DeleteRecordAndCollectionAsync<TKey>(
        IVectorStoreRecordCollection<TKey, Hotel<TKey>> vectorRecordStore,
        ITextEmbeddingGenerationService embeddingGenerationService,
        TKey recordKey)
    {
        // Delete Record.
        await vectorRecordStore.DeleteAsync(recordKey);

        // Delete collection.
        await vectorRecordStore.DeleteCollectionAsync();
    }

    private sealed class DocumentationSnippet
    {
        [VectorStoreRecordKey]
        public ulong key { get; init; }

        [VectorStoreRecordData]
        public string location { get; init; }

        [VectorStoreRecordData]
        public string text { get; init; }

        [VectorStoreRecordData]
        public string url { get; init; }

        [VectorStoreRecordData]
        public List<string> sections { get; init; }

        [VectorStoreRecordData]
        public List<string> titles { get; init; }

        [VectorStoreRecordVector]
        public ReadOnlyMemory<float>? embedding { get; init; }
    }

    [Fact]
    public async Task RunReuseExampleAsync()
    {
        // Create docker containers.
        using var dockerClientConfiguration = new DockerClientConfiguration();
        var client = dockerClientConfiguration.CreateClient();
        var qdrantContainerId = await VectorStoreInfra.SetupQdrantContainerAsync(client);

        // Create the qdrant vector store client.
        var qdrantClient = new QdrantClient("localhost");
        var qdrantRecordStore = new QdrantVectorStoreRecordCollection<DocumentationSnippet>(qdrantClient, "docs");

        // Get record.
        var docSnippet = await qdrantRecordStore.GetAsync(0, new() { IncludeVectors = true });

        // Delete docker containers.
        await VectorStoreInfra.DeleteContainerAsync(client, qdrantContainerId);
    }

    private sealed class DataReference
    {
        public ulong Key { get; init; }

        public string Reference { get; init; }

        public ReadOnlyMemory<float>? Embedding { get; init; }
    }

    [Fact]
    public async Task RunConfiguredCreateExampleAsync()
    {
        // Create docker containers.
        using var dockerClientConfiguration = new DockerClientConfiguration();
        var client = dockerClientConfiguration.CreateClient();
        var qdrantContainerId = await VectorStoreInfra.SetupQdrantContainerAsync(client);

        // Create Embedding Service
        var embeddingService = new AzureOpenAITextEmbeddingGenerationService(
                TestConfiguration.AzureOpenAIEmbeddings.DeploymentName,
                TestConfiguration.AzureOpenAIEmbeddings.Endpoint,
                TestConfiguration.AzureOpenAIEmbeddings.ApiKey);

        // Create collection definition.
        var refsCollectionDefinition = new VectorStoreRecordDefinition
        {
            Properties = new List<VectorStoreRecordProperty>
            {
                new VectorStoreRecordKeyProperty("Key"),
                new VectorStoreRecordDataProperty("Reference"),
                new VectorStoreRecordVectorProperty("Embedding") { Dimensions = 1536 }
            }
        };

        // Create the qdrant configured collection store client.
        var qdrantClient = new QdrantClient("localhost");

        IVectorStore qdrantVectorstore = new QdrantVectorStore(qdrantClient, new() { HasNamedVectors = true });

        // Create collection and add data.
        await ConfiguredCreateExampleAsync(qdrantVectorstore, embeddingService, "refs", refsCollectionDefinition);

        // Search Vector DB.
        var question = "What is Azure AI Search?";
        var questionEmbeddings = await embeddingService.GenerateEmbeddingsAsync(new List<string> { question });
        var searchResult = await qdrantClient.SearchAsync("refs", questionEmbeddings.First(), vectorName: "Embedding");

        // Delete docker containers.
        await VectorStoreInfra.DeleteContainerAsync(client, qdrantContainerId);
    }

    private async Task ConfiguredCreateExampleAsync(
        IVectorStore vectorStore,
        ITextEmbeddingGenerationService embeddingGenerationService,
        string collectionName,
        VectorStoreRecordDefinition refsCollectionDefinition)
    {
        var vectorStoreCollection = vectorStore.GetCollection<ulong, DataReference>(collectionName, refsCollectionDefinition);
        await vectorStoreCollection.CreateCollectionIfNotExistsAsync();

        // Generate Embeddings.
        var description = """
            What is Azure AI Search?
            Azure AI Search provides a dedicated search engine and persistent storage of your searchable content for full text and vector search scenarios.It also includes optional, integrated AI to extract more text and structure from raw content, and to chunk and vectorize content for vector search.
""";
        var embeddings = await embeddingGenerationService.GenerateEmbeddingsAsync(new List<string> { description });

        // Upsert.
        await vectorStoreCollection.UpsertAsync(
            new DataReference
            {
                Key = 0,
                Reference = "https://learn.microsoft.com/en-us/azure/search/search-faq-frequently-asked-questions#what-is-azure-ai-search-",
                Embedding = embeddings.First()
            });
    }

    private sealed class RedisVectorStoreFactory : IRedisVectorStoreRecordCollectionFactory
    {
        public IVectorStoreRecordCollection<TKey, TRecord> CreateVectorStoreRecordCollection<TKey, TRecord>(IDatabase database, string name, VectorStoreRecordDefinition? vectorStoreRecordDefinition) where TRecord : class
        {
            var store = new RedisVectorStoreRecordCollection<TRecord>(database, name, new() { VectorStoreRecordDefinition = vectorStoreRecordDefinition, PrefixCollectionNameToKeyNames = true }) as IVectorStoreRecordCollection<TKey, TRecord>;
            return store!;
        }
    }

    private sealed class QdrantVectorStoreFactory : IQdrantVectorStoreRecordCollectionFactory
    {
        public IVectorStoreRecordCollection<TKey, TRecord> CreateVectorStoreRecordCollection<TKey, TRecord>(QdrantClient qdrantClient, string name, VectorStoreRecordDefinition? vectorStoreRecordDefinition) where TRecord : class
        {
            var store = new QdrantVectorStoreRecordCollection<TRecord>(qdrantClient, name, new() { VectorStoreRecordDefinition = vectorStoreRecordDefinition, HasNamedVectors = true }) as IVectorStoreRecordCollection<TKey, TRecord>;
            return store!;
        }
    }

    [Fact]
    public async Task RunFactoryOptionAsync()
    {
        // Create docker containers.
        using var dockerClientConfiguration = new DockerClientConfiguration();
        var client = dockerClientConfiguration.CreateClient();
        var qdrantContainerId = await VectorStoreInfra.SetupQdrantContainerAsync(client);
        var redisContainerId = await VectorStoreInfra.SetupRedisContainerAsync(client);

        // Create Embedding Service
        var embeddingService = new AzureOpenAITextEmbeddingGenerationService(
                TestConfiguration.AzureOpenAIEmbeddings.DeploymentName,
                TestConfiguration.AzureOpenAIEmbeddings.Endpoint,
                TestConfiguration.AzureOpenAIEmbeddings.ApiKey);

        // Create the redis vector store clients.
        ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("localhost:6379");
        var redisDatabase = redis.GetDatabase();
        var redisVectorStore = new TextEmbeddingVectorStore(new RedisVectorStore(redisDatabase, new() { VectorStoreCollectionFactory = new RedisVectorStoreFactory() }), embeddingService);

        // Create the qdrant vector store clients.
        var qdrantClient = new QdrantClient("localhost");
        var qdrantVectorStore = new TextEmbeddingVectorStore(new QdrantVectorStore(qdrantClient, new() { HasNamedVectors = true, VectorStoreCollectionFactory = new QdrantVectorStoreFactory() }), embeddingService);

        await RunFactorySampleAsync(redisVectorStore, "parpat");
        await RunFactorySampleAsync(qdrantVectorStore, 5ul);

        // Delete docker containers.
        await VectorStoreInfra.DeleteContainerAsync(client, qdrantContainerId);
        await VectorStoreInfra.DeleteContainerAsync(client, redisContainerId);
    }

    private static async Task RunFactorySampleAsync<TKey>(IVectorStore vectorStore, TKey recordKey)
    {
        // Example 1: Create collection and upsert.
        var recordCollection = vectorStore.GetCollection<TKey, Hotel<TKey>>("hotels");
        await recordCollection.CreateCollectionIfNotExistsAsync();

        await recordCollection.UpsertAsync(
            new Hotel<TKey>
            {
                HotelId = recordKey,
                HotelName = "Paradise Patch",
                HotelRating = 4.5f,
                ParkingIncluded = true,
                Description = "A magical fusion of hotel and beach."
            });

        var retrievedHotel = await recordCollection.GetAsync(recordKey, new() { IncludeVectors = true });

        // Example 2: Get collection and delete record.
        var existingCollection = vectorStore.GetCollection<TKey, Hotel<TKey>>("hotels");
        await existingCollection.DeleteAsync(recordKey);

        // Example 3: Delete collection.
        await existingCollection.DeleteCollectionAsync();
    }
}

#pragma warning restore CA1859 // Use concrete types when possible for improved performance
