// Copyright (c) Microsoft. All rights reserved.

using Docker.DotNet;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel.Connectors.Redis;
using Microsoft.SemanticKernel.Memory;
using Qdrant.Client;
using StackExchange.Redis;

namespace Memory;

public class VectorStore_BackwardCompatible(ITestOutputHelper output) : BaseTest(output)
{
    [Fact]
    public async Task RunCreateExampleAsync()
    {
        // Create docker containers.
        using var dockerClientConfiguration = new DockerClientConfiguration();
        var client = dockerClientConfiguration.CreateClient();
        ////var qdrantContainerId = await VectorStore_Infra.SetupQdrantContainerAsync(client);
        var redisContainerId = await VectorStore_Infra.SetupRedisContainerAsync(client);

        // Create the qdrant vector store clients.
        ////var qdrantClient = new QdrantClient("localhost");
        ////var qdrantCollectionStore = new QdrantVectorCollectionStore(qdrantClient, QdrantVectorCollectionCreate.Create<Hotel<ulong>>(qdrantClient));
        ////var qdrantRecordStore = new QdrantVectorRecordStore<Hotel<ulong>>(qdrantClient);

        // Create the redis vector store clients.
        ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("localhost:6379");
        var redisDatabase = redis.GetDatabase();
        var redisMemoryStore = new RedisMemoryStore(redisDatabase);
        ///var redisRecordStore = new RedisVectorRecordStore<MemoryRecord>(redisDatabase);

        // Create collection and add data.
        await redisMemoryStore.CreateCollectionAsync("testCollection");
        var memoryRecord = new MemoryRecord(new MemoryRecordMetadata(false, "testId", "Text", "Description", "eternalSourceName", "AdditionalMetadata"), new ReadOnlyMemory<float>(new float[] { 1, 2, 3, 4 }), "testKey", DateTimeOffset.Now);
        await redisMemoryStore.UpsertAsync("testCollection", memoryRecord);

        // Delete docker containers.
        ////await VectorStore_Infra.DeleteContainerAsync(client, qdrantContainerId);
        await VectorStore_Infra.DeleteContainerAsync(client, redisContainerId);
    }
}
