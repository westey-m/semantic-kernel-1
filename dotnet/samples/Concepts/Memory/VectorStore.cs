// Copyright (c) Microsoft. All rights reserved.

using System.Runtime.InteropServices;
using Docker.DotNet;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel.Connectors.Redis;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Embeddings;
using MongoDB.Driver;
using NRedisStack.RedisStackCommands;
using Qdrant.Client;
using StackExchange.Redis;
using NRedisStack.Search;

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
        var qdrantContainerId = await VectorStore_Infra.SetupQdrantContainerAsync(client);
        var redisContainerId = await VectorStore_Infra.SetupRedisContainerAsync(client);

        // Create the qdrant vector store clients.
        var qdrantClient = new QdrantClient("localhost");
        var qdrantCollectionStore = new QdrantVectorCollectionStore(qdrantClient, QdrantVectorCollectionCreate.Create<Hotel<ulong>>(qdrantClient));
        var qdrantRecordStore = new QdrantVectorRecordStore<Hotel<ulong>>(qdrantClient, "hotels");

        // Create the redis vector store clients.
        ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("localhost:6379");
        var redisDatabase = redis.GetDatabase();
        var redisCollectionStore = new RedisVectorCollectionStore(redisDatabase, RedisVectorCollectionCreate.Create<Hotel<string>>(redisDatabase));
        var redisRecordStore = new RedisVectorRecordStore<Hotel<string>>(redisDatabase, "hotels", new() { PrefixCollectionNameToKeyNames = true });

        // Create Embedding Service
        var embeddingService = new AzureOpenAITextEmbeddingGenerationService(
                TestConfiguration.AzureOpenAIEmbeddings.DeploymentName,
                TestConfiguration.AzureOpenAIEmbeddings.Endpoint,
                TestConfiguration.AzureOpenAIEmbeddings.ApiKey);

        // Create collection and add data.
        await this.CreateCollectionAndAddDataAsync(redisCollectionStore, redisRecordStore, embeddingService, "testRecord");
        await this.CreateCollectionAndAddDataAsync(qdrantCollectionStore, qdrantRecordStore, embeddingService, 5ul);

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
        await this.DeleteRecordAndCollectionAsync(redisCollectionStore, redisRecordStore, embeddingService, "testRecord");
        await this.DeleteRecordAndCollectionAsync(qdrantCollectionStore, qdrantRecordStore, embeddingService, 5ul);

        // Delete docker containers.
        await VectorStore_Infra.DeleteContainerAsync(client, qdrantContainerId);
        await VectorStore_Infra.DeleteContainerAsync(client, redisContainerId);
    }

    private async Task CreateCollectionAndAddDataAsync<TKey>(
        IVectorCollectionStore collectionStore,
        IVectorRecordStore<TKey, Hotel<TKey>> vectorRecordStore,
        ITextEmbeddingGenerationService embeddingGenerationService,
        TKey recordKey)
    {
        // Create collection.
        await collectionStore.CreateCollectionAsync("hotels");

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
        IVectorCollectionStore collectionStore,
        IVectorRecordStore<TKey, Hotel<TKey>> vectorRecordStore,
        ITextEmbeddingGenerationService embeddingGenerationService,
        TKey recordKey)
    {
        // Delete Record.
        await vectorRecordStore.DeleteAsync(recordKey);

        // Delete collection.
        await collectionStore.DeleteCollectionAsync("hotels");
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
        var qdrantContainerId = await VectorStore_Infra.SetupQdrantContainerAsync(client);

        // Create the qdrant vector store client.
        var qdrantClient = new QdrantClient("localhost");
        var qdrantRecordStore = new QdrantVectorRecordStore<DocumentationSnippet>(qdrantClient, "docs");

        // Get record.
        var docSnippet = await qdrantRecordStore.GetAsync(0, new() { IncludeVectors = true });

        // Delete docker containers.
        await VectorStore_Infra.DeleteContainerAsync(client, qdrantContainerId);
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
        var qdrantContainerId = await VectorStore_Infra.SetupQdrantContainerAsync(client);

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

        IConfiguredVectorCollectionStore configuredQdrantCollectionStore = new QdrantVectorCollectionStore(qdrantClient, QdrantVectorCollectionCreate.Create(qdrantClient, new() { HasNamedVectors = true }));
        IVectorCollectionStore qdrantCollectionStore = new QdrantVectorCollectionStore(qdrantClient, QdrantVectorCollectionCreate.Create(qdrantClient, refsCollectionDefinition, new() { HasNamedVectors = true }));

        IVectorCollectionStore productsCollectionStore = new QdrantVectorCollectionStore(qdrantClient, QdrantVectorCollectionCreate.Create(qdrantClient, refsCollectionDefinition, new() { HasNamedVectors = true }));
        IVectorCollectionStore usersCollectionStore = new QdrantVectorCollectionStore(qdrantClient, QdrantVectorCollectionCreate.Create(qdrantClient, refsCollectionDefinition, new() { HasNamedVectors = true }));

        var qdrantRecordStore = new QdrantVectorRecordStore<DataReference>(qdrantClient, "refs", new() { VectorStoreRecordDefinition = refsCollectionDefinition, HasNamedVectors = true });

        // Create collection and add data.
        await ConfiguredCreateExampleAsync(configuredQdrantCollectionStore, qdrantCollectionStore, qdrantRecordStore, embeddingService, "refs", refsCollectionDefinition);

        // Search Vector DB.
        var question = "What is Azure AI Search?";
        var questionEmbeddings = await embeddingService.GenerateEmbeddingsAsync(new List<string> { question });
        var searchResult = await qdrantClient.SearchAsync("refs", questionEmbeddings.First(), vectorName: "Embedding");

        // Delete docker containers.
        await VectorStore_Infra.DeleteContainerAsync(client, qdrantContainerId);
    }

    private async Task ConfiguredCreateExampleAsync(
        IConfiguredVectorCollectionStore configuredCollectionStore,
        IVectorCollectionStore collectionStore,
        IVectorRecordStore<ulong, DataReference> vectorRecordStore,
        ITextEmbeddingGenerationService embeddingGenerationService,
        string collectionName,
        VectorStoreRecordDefinition refsCollectionDefinition)
    {
        // With configuration as method parameter, so collection store is mostly schema agnostic.
        if (!await configuredCollectionStore.CollectionExistsAsync(collectionName))
        {
            await configuredCollectionStore.CreateCollectionAsync(collectionName, refsCollectionDefinition);
        }

        // With configuration from type on method, so collection store is mostly schema agnostic.
        if (!await configuredCollectionStore.CollectionExistsAsync(collectionName))
        {
            await configuredCollectionStore.CreateCollectionAsync<DataReference>(collectionName);
        }

        // With configuration as constructor parameter, so collection store is schema specific.
        if (!await collectionStore.CollectionExistsAsync(collectionName))
        {
            await collectionStore.CreateCollectionAsync(collectionName);
        }

        // Generate Embeddings.
        var description = """
            What is Azure AI Search?
            Azure AI Search provides a dedicated search engine and persistent storage of your searchable content for full text and vector search scenarios.It also includes optional, integrated AI to extract more text and structure from raw content, and to chunk and vectorize content for vector search.
""";
        var embeddings = await embeddingGenerationService.GenerateEmbeddingsAsync(new List<string> { description });

        // Upsert.
        await vectorRecordStore.UpsertAsync(
            new DataReference
            {
                Key = 0,
                Reference = "https://learn.microsoft.com/en-us/azure/search/search-faq-frequently-asked-questions#what-is-azure-ai-search-",
                Embedding = embeddings.First()
            });
    }

    private sealed class RedisVectorStoreFactory : IRedisVectorRecordStoreFactory
    {
        public IVectorRecordStore<TKey, TRecord> CreateRecordStore<TKey, TRecord>(IDatabase database, string name, VectorStoreRecordDefinition? vectorStoreRecordDefinition) where TRecord : class
        {
            var store = new RedisVectorRecordStore<TRecord>(database, name, new() { VectorStoreRecordDefinition = vectorStoreRecordDefinition, PrefixCollectionNameToKeyNames = true }) as IVectorRecordStore<TKey, TRecord>;
            return store!;
        }
    }

    private sealed class QdrantVectorStoreFactory : IQdrantVectorRecordStoreFactory
    {
        public IVectorRecordStore<TKey, TRecord> CreateRecordStore<TKey, TRecord>(QdrantClient qdrantClient, string name, VectorStoreRecordDefinition? vectorStoreRecordDefinition) where TRecord : class
        {
            var store = new QdrantVectorRecordStore<TRecord>(qdrantClient, name, new() { VectorStoreRecordDefinition = vectorStoreRecordDefinition, HasNamedVectors = true }) as IVectorRecordStore<TKey, TRecord>;
            return store!;
        }
    }

    [Fact]
    public async Task RunFactoryOptionAsync()
    {
        // Create docker containers.
        using var dockerClientConfiguration = new DockerClientConfiguration();
        var client = dockerClientConfiguration.CreateClient();
        var qdrantContainerId = await VectorStore_Infra.SetupQdrantContainerAsync(client);
        var redisContainerId = await VectorStore_Infra.SetupRedisContainerAsync(client);

        // Create Embedding Service
        var embeddingService = new AzureOpenAITextEmbeddingGenerationService(
                TestConfiguration.AzureOpenAIEmbeddings.DeploymentName,
                TestConfiguration.AzureOpenAIEmbeddings.Endpoint,
                TestConfiguration.AzureOpenAIEmbeddings.ApiKey);

        // Create the redis vector store clients.
        ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("localhost:6379");
        var redisDatabase = redis.GetDatabase();
        var redisVectorStore = new TextEmbeddingVectorStore(new RedisVectorCollectionStore(redisDatabase, RedisVectorCollectionCreate.Create(redisDatabase), new RedisVectorStoreFactory()), embeddingService);

        // Create the qdrant vector store clients.
        var qdrantClient = new QdrantClient("localhost");
        var qdrantVectorStore = new TextEmbeddingVectorStore(new QdrantVectorCollectionStore(qdrantClient, QdrantVectorCollectionCreate.Create(qdrantClient, new() { HasNamedVectors = true }), new QdrantVectorStoreFactory()), embeddingService);

        await RunFactorySampleAsync(redisVectorStore, "parpat");
        await RunFactorySampleAsync(qdrantVectorStore, 5ul);

        // Delete docker containers.
        await VectorStore_Infra.DeleteContainerAsync(client, qdrantContainerId);
        await VectorStore_Infra.DeleteContainerAsync(client, redisContainerId);
    }

    private static async Task RunFactorySampleAsync<TKey>(IVectorStore vectorStore, TKey recordKey)
    {
        // Example 1: Create collection and upsert.
        var recordCollection = await vectorStore.CreateCollectionAsync<TKey, Hotel<TKey>>("hotels");
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
        await vectorStore.DeleteCollectionAsync("hotels");
    }
}

#pragma warning restore CA1859 // Use concrete types when possible for improved performance
