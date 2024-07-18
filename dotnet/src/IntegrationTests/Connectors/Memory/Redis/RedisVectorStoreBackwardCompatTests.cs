// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Redis;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Memory;
using StackExchange.Redis;
using Xunit;

namespace SemanticKernel.IntegrationTests.Connectors.Memory.Redis;

/// <summary>
/// Tests the backwards compatibility layer of <see cref="RedisVectorStoreRecordCollection{TRecord}"/> with the <see cref="RedisMemoryStore"/>.
/// </summary>
/// <param name="fixture">The test fixture.</param>
[Collection("RedisVectorStoreCollection")]
public class RedisVectorStoreBackwardCompatTests(RedisVectorStoreFixture fixture)
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ItCanReadDataWrittenByMemoryStoreAsync(bool isReference)
    {
        // Arrange.
        var testCollectionName = "backcompattest";
        var testId = "testid";

        // Arrange legacy memory store.
        using var memoryStore = new RedisMemoryStore(fixture.Database);

        // Arrange new vector store.
        var kernelBuilder = Kernel
            .CreateBuilder()
            .AddRedisMemoryVectorStoreRecordCollection(testCollectionName);
        kernelBuilder.Services.AddTransient<IDatabase>((sp) => fixture.Database);
        var kernel = kernelBuilder.Build();

        var sut = kernel.GetRequiredService<IVectorStoreRecordCollection<string, MemoryRecord>>();

        // Arrange collection with test data using legacy memory store.
        if (!await memoryStore.DoesCollectionExistAsync(testCollectionName))
        {
            await memoryStore.CreateCollectionAsync(testCollectionName);
        }

        await memoryStore.UpsertAsync(
            testCollectionName,
            new MemoryRecord(
                new MemoryRecordMetadata(isReference, testId, "testtext", "testdescription", "testexternalsourcename", "testadditionalmetadata"),
                new[] { 30f, 31f, 32f, 33f },
                "testKey"));
        var expected = await memoryStore.GetAsync(testCollectionName, testId, true);

        // Act.
        var actual = await sut.GetAsync(testId, new GetRecordOptions { IncludeVectors = true });

        // Assert.
        Assert.NotNull(actual);
        Assert.Equal(expected!.Key, actual.Key);
        Assert.Equal(expected.Metadata.Id, actual.Metadata.Id);
        Assert.Equal(expected.Metadata.Text, actual.Metadata.Text);
        Assert.Equal(expected.Metadata.Description, actual.Metadata.Description);
        Assert.Equal(expected.Metadata.AdditionalMetadata, actual.Metadata.AdditionalMetadata);
        Assert.Equal(expected.Metadata.ExternalSourceName, actual.Metadata.ExternalSourceName);
        Assert.Equal(expected.Metadata.IsReference, actual.Metadata.IsReference);
        Assert.Equal(expected.Embedding.Span.ToArray(), actual.Embedding.Span.ToArray());

        // Cleanup.
        await memoryStore.DeleteCollectionAsync(testCollectionName);
    }
}
