// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Redis;
using Microsoft.SemanticKernel.Data;
using StackExchange.Redis;

namespace Memory;

/// <summary>
/// Contains an example showing how to do data ingestion using <see cref="IVectorStore"/>.
/// </summary>
public class VectorStore_DataIngestion(ITestOutputHelper output) : BaseTest(output), IClassFixture<VectorStore_RedisContainer_Fixture>
{
    [Fact]
    public async Task ExampleMainAsync()
    {
        // Use the kernel for DI purposes.
        var kernelBuilder = Kernel
            .CreateBuilder();

        // Register a embedding generation service with the DI container.
        kernelBuilder.AddAzureOpenAITextEmbeddingGeneration(
            deploymentName: TestConfiguration.AzureOpenAIEmbeddings.DeploymentName,
            endpoint: TestConfiguration.AzureOpenAIEmbeddings.Endpoint,
            apiKey: TestConfiguration.AzureOpenAIEmbeddings.ApiKey);

        // Register the Redis vector store with the DI container.
        RegisterRedisWithDIContainer(kernelBuilder);

        // Register the DataIngestor with the DI container.
        kernelBuilder.Services.AddTransient<DataIngestor>();

        // Build the kernel.
        var kernel = kernelBuilder.Build();

        // Build a DataIngestor object using the DI container and start ingestion.
        var dataIngestor = kernel.GetRequiredService<DataIngestor>();
        var upsertedKeys = await dataIngestor.ImportDataAsync();

        // Get one of the upserted records.
        var upsertedRecord = await dataIngestor.GetGlossaryAsync(upsertedKeys.First());

        output.WriteLine($"Upserted keys: {string.Join(", ", upsertedKeys)}");
        output.WriteLine($"Upserted record: {JsonSerializer.Serialize(upsertedRecord)}");
    }

    private void RegisterRedisWithDIContainer(IKernelBuilder kernelBuilder)
    {
        // Register a redis client with the DI container.
        kernelBuilder.Services.AddTransient<IDatabase>((sp) =>
        {
            ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("localhost:6379");
            return redis.GetDatabase();
        });

        // Register the Redis vector store with the DI conatiner.
        kernelBuilder.AddRedisVectorStore(generateEmbeddings: true);
    }

    /// <summary>
    /// Sample class that does ingestion of data into a vector store.
    /// </summary>
    /// <param name="vectorStore">The vector store to ingest data into.</param>
    private class DataIngestor(IVectorStore vectorStore)
    {
        public async Task<IEnumerable<string>> ImportDataAsync()
        {
            var collection = vectorStore.GetCollection<string, Glossary<string>>("skglossary");
            await collection.CreateCollectionIfNotExistsAsync();
            var upsertedKeys = GenerateGlossaryEntries().Select(x => collection.UpsertAsync(x));
            return await Task.WhenAll(upsertedKeys);
        }

        public Task<Glossary<string>?> GetGlossaryAsync(string key)
        {
            var collection = vectorStore.GetCollection<string, Glossary<string>>("skglossary");
            return collection.GetAsync(key, new() { IncludeVectors = true });
        }

        public IEnumerable<Glossary<string>> GenerateGlossaryEntries()
        {
            yield return new Glossary<string>
            {
                Key = "API",
                Term = "API",
                Definition = "Application Programming Interface. A set of rules and specifications that allow software components to communicate and exchange data."
            };

            yield return new Glossary<string>
            {
                Key = "Connectors",
                Term = "Connectors",
                Definition = "Connectors allow you to integrate with various services provide AI capabilities, including LLM, AudioToText, TextToAudio, Embedding generation, etc."
            };

            yield return new Glossary<string>
            {
                Key = "RAG",
                Term = "RAG",
                Definition = "Retrieval Augmented Generation - a term that refers to the process of retrieving additional data to provide as context to an LLM to use when generating a response (completion) to a user’s question (prompt)."
            };
        }
    }

    private class Glossary<TKey>
    {
        [VectorStoreRecordKey]
        public TKey Key { get; set; }

        [VectorStoreRecordData]
        public string Term { get; set; }

        [VectorStoreRecordData(HasEmbedding = true, EmbeddingPropertyName = nameof(DefinitionEmbedding))]
        public string Definition { get; set; }

        [VectorStoreRecordVector(1536)]
        public ReadOnlyMemory<float> DefinitionEmbedding { get; set; }
    }
}
