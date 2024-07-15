// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Connectors.AzureAISearch;
using Xunit;
using Xunit.Abstractions;

namespace SemanticKernel.IntegrationTests.Connectors.Memory.AzureAISearch;

[Collection("AzureAISearchVectorStoreCollection")]
public class AzureAISearchVectorStoreTests(ITestOutputHelper output, AzureAISearchVectorStoreFixture fixture) : IClassFixture<AzureAISearchVectorStoreFixture>
{
    // If null, all tests will be enabled
    private const string SkipReason = null; //"Requires Azure AI Search Service instance up and running";

    [Theory(Skip = SkipReason)]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ItCanCreateACollectionAsync(bool useDefinition)
    {
        // Arrange
        var testCollectionName = $"{fixture.TestIndexName}-createtest";
        var sut = new AzureAISearchVectorStore(fixture.SearchIndexClient);
        var collection = sut.GetCollection<string, AzureAISearchVectorStoreFixture.Hotel>(testCollectionName);
        await collection.DeleteCollectionAsync();

        // Act
        await sut.CreateCollectionAsync<string, AzureAISearchVectorStoreFixture.Hotel>(testCollectionName, useDefinition ? fixture.VectorStoreRecordDefinition : null);

        // Assert
        var existResult = await collection.CollectionExistsAsync();
        Assert.True(existResult);
        await collection.DeleteCollectionAsync();

        // Output
        output.WriteLine(existResult.ToString());
    }

    [Fact(Skip = SkipReason)]
    public async Task ItCanGetAListOfExistingCollectionNamesAsync()
    {
        // Arrange
        var additionalCollectionName = fixture.TestIndexName + "-listnames";
        await AzureAISearchVectorStoreFixture.DeleteIndexIfExistsAsync(additionalCollectionName, fixture.SearchIndexClient);
        await AzureAISearchVectorStoreFixture.CreateIndexAsync(additionalCollectionName, fixture.SearchIndexClient);
        var sut = new AzureAISearchVectorStore(fixture.SearchIndexClient);

        // Act
        var collectionNames = await sut.ListCollectionNamesAsync().ToListAsync();

        // Assert
        Assert.Equal(2, collectionNames.Where(x => x.StartsWith(fixture.TestIndexName, StringComparison.InvariantCultureIgnoreCase)).Count());
        Assert.Contains(fixture.TestIndexName, collectionNames);
        Assert.Contains(additionalCollectionName, collectionNames);

        // Output
        output.WriteLine(string.Join(",", collectionNames));

        // Cleanup
        await AzureAISearchVectorStoreFixture.DeleteIndexIfExistsAsync(additionalCollectionName, fixture.SearchIndexClient);
    }
}
