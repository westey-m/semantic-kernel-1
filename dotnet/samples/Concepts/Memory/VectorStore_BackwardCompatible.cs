// Copyright (c) Microsoft. All rights reserved.

using Docker.DotNet;
using Memory.VectorStoreFixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Redis;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Memory;
using StackExchange.Redis;

namespace Memory;

/// <summary>
/// Shows how to construct and use <see cref="IVectorStore"/> implementations in a way that allows for backward compatibility with the legacy <see cref="IMemoryStore"/> implementations.
/// </summary>
public class VectorStore_BackwardCompatible(ITestOutputHelper output) : BaseTest(output)
{
    [Fact]
    public async Task RedisBackwardCompatibleExampleAsync()
    {
        var testCollectionName = "backcompattest";
        var testId = "testid";

        // Start up a Redis docker container.
        using var dockerClientConfiguration = new DockerClientConfiguration();
        var client = dockerClientConfiguration.CreateClient();
        var redisContainerId = await VectorStoreInfra.SetupRedisContainerAsync(client);

        // Use the kernel for DI purposes.
        var kernelBuilder = Kernel
            .CreateBuilder();

        // Register a redis client with DI container.
        kernelBuilder.Services.AddTransient<IDatabase>((sp) =>
        {
            ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("localhost:6379");
            return redis.GetDatabase();
        });

        // Register the legacy Redis memory store with DI container.
        kernelBuilder.Services.AddTransient<IMemoryStore, RedisMemoryStore>();

        // Register the backwards compatible Redis vector store with DI conatiner.
        kernelBuilder.AddRedisMemoryVectorStoreRecordCollection(testCollectionName);

        // Register the writer and reader with DI container.
        kernelBuilder.Services.AddTransient<MemoryStoreUser>();

        // Build the kernel.
        var kernel = kernelBuilder.Build();

        // Build a MemoryStoreUser object using the DI container.
        var memoryStoreUser = kernel.GetRequiredService<MemoryStoreUser>();

        // Write a record to the collection using the Legacy MemoryStore.
        var memoryRecord = new MemoryRecord(new MemoryRecordMetadata(false, testId, "Text", "Description", "eternalSourceName", "AdditionalMetadata"), new ReadOnlyMemory<float>(new float[] { 1, 2, 3, 4 }), "testKey", DateTimeOffset.Now);
        await memoryStoreUser.WriteAsync(testCollectionName, memoryRecord);

        // Read the record from the collection using the new backwards compatible VectorStore.
        var recordText = await memoryStoreUser.ReadRecordTextAsync(testId);
        Output.WriteLine(recordText);

        // Delete docker container.
        await VectorStoreInfra.DeleteContainerAsync(client, redisContainerId);
    }

    /// <summary>
    /// Sample class that uses both the legacy <see cref="IMemoryStore"/> and a backwards compatible instance of <see cref="IVectorStoreRecordCollection{TKey, TRecord}"/>.
    /// </summary>
    /// <param name="memoryStore">The legacy <see cref="IMemoryStore"/> instance.</param>
    /// <param name="vectorStoreRecordCollection">The backwards compatible <see cref="IVectorStoreRecordCollection{TKey, TRecord}"/> instance.</param>
    private sealed class MemoryStoreUser(IMemoryStore memoryStore, IVectorStoreRecordCollection<string, MemoryRecord> vectorStoreRecordCollection)
    {
        public async Task WriteAsync(string collectionName, MemoryRecord record)
        {
            if (!await memoryStore.DoesCollectionExistAsync(collectionName))
            {
                await memoryStore.CreateCollectionAsync(collectionName);
            }

            await memoryStore.UpsertAsync(collectionName, record);
        }

        public async Task<string> ReadRecordTextAsync(string key)
        {
            var record = await vectorStoreRecordCollection.GetAsync(key);
            return record!.Metadata.Text;
        }
    }
}
