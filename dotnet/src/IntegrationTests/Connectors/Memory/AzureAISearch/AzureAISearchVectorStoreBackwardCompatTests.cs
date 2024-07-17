// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;
using Xunit;
using Microsoft.SemanticKernel.Connectors.AzureAISearch;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;
using Azure.Search.Documents.Indexes;

namespace SemanticKernel.IntegrationTests.Connectors.Memory.AzureAISearch;

/// <summary>
/// Tests the backwards compatibility layer of <see cref="AzureAISearchVectorStoreRecordCollection{TRecord}"/> with the <see cref="AzureAISearchMemoryStore"/>.
/// </summary>
/// <param name="fixture">The test fixture.</param>
[Collection("AzureAISearchVectorStoreCollection")]
public class AzureAISearchVectorStoreBackwardCompatTests(AzureAISearchVectorStoreFixture fixture)
{
    [Fact]
    public async Task ItCanReadDataWrittenByMemoryStoreAsync()
    {
        // Arrange.
        var testCollectionName = $"{fixture.TestIndexName}-backcompattest";
        var testId = "testid";
        var memoryStore = new AzureAISearchMemoryStore(fixture.SearchIndexClient);

        var kernelBuilder = Kernel
            .CreateBuilder()
            .AddAzureAISearchMemoryVectorStoreRecordCollection(testCollectionName);
        kernelBuilder.Services.AddTransient<SearchIndexClient>((sp) => fixture.SearchIndexClient);
        var kernel = kernelBuilder.Build();

        var sut = kernel.GetRequiredService<IVectorStoreRecordCollection<string, MemoryRecord>>();

        if (!await memoryStore.DoesCollectionExistAsync(testCollectionName))
        {
            await memoryStore.CreateCollectionAsync(testCollectionName);
        }

        await memoryStore.UpsertAsync(
            testCollectionName,
            new MemoryRecord(
                new MemoryRecordMetadata(false, testId, "testtext", "testdescription", "testexternalsourcename", "testadditionalmetadata"),
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
