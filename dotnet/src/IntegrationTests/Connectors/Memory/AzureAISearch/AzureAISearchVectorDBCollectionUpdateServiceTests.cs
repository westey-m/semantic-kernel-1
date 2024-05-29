// Copyright (c) Microsoft. All rights reserved.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Connectors.AzureAISearch;
using Xunit;
using Xunit.Abstractions;

namespace SemanticKernel.IntegrationTests.Connectors.Memory.AzureAISearch;

[Collection("AzureAISearchVectorDBCollection")]
public class AzureAISearchVectorDBCollectionUpdateServiceTests(ITestOutputHelper output, AzureAISearchVectorDBFixture fixture) : IClassFixture<AzureAISearchVectorDBFixture>
{
    // If null, all tests will be enabled
    private const string SkipReason = null; //"Requires Azure AI Search Service instance up and running";

    [Fact(Skip = SkipReason)]
    public async Task ItCanCheckIfCollectionExistsForExistingCollectionAsync()
    {
        // Arrange
        var sut = new AzureAISearchVectorDBCollectionUpdateService(fixture.SearchIndexClient);

        // Act
        var existResult = await sut.CollectionExistsAsync(fixture.TestIndexName);

        // Assert
        Assert.True(existResult);

        // Output
        output.WriteLine(existResult.ToString());
    }

    [Fact(Skip = SkipReason)]
    public async Task ItCanCheckIfCollectionExistsForNonExistingCollectionAsync()
    {
        // Arrange
        var sut = new AzureAISearchVectorDBCollectionUpdateService(fixture.SearchIndexClient);

        // Act
        var existResult = await sut.CollectionExistsAsync("non-existing-collection");

        // Assert
        Assert.False(existResult);

        // Output
        output.WriteLine(existResult.ToString());
    }

    [Fact(Skip = SkipReason)]
    public async Task ItCanGetAListOfExistingCollectionNamesAsync()
    {
        // Arrange
        await AzureAISearchVectorDBFixture.DeleteIndexIfExistsAsync(fixture.TestIndexName + "-additional", fixture.SearchIndexClient);
        await AzureAISearchVectorDBFixture.CreateIndexAsync(fixture.TestIndexName + "-additional", fixture.SearchIndexClient);
        var sut = new AzureAISearchVectorDBCollectionUpdateService(fixture.SearchIndexClient);

        // Act
        var collectionNames = await sut.ListCollectionNamesAsync().ToListAsync();

        // Assert
        Assert.Equal(2, collectionNames.Count);
        Assert.Contains(fixture.TestIndexName, collectionNames);
        Assert.Contains(fixture.TestIndexName + "-additional", collectionNames);

        // Output
        output.WriteLine(string.Join(",", collectionNames));

        // Cleanup
        await AzureAISearchVectorDBFixture.DeleteIndexIfExistsAsync(fixture.TestIndexName + "-additional", fixture.SearchIndexClient);
    }

    [Fact(Skip = SkipReason)]
    public async Task ItCanDeleteACollectionAsync()
    {
        // Arrange
        var tempCollectionName = fixture.TestIndexName + "-temp";
        await AzureAISearchVectorDBFixture.CreateIndexAsync(tempCollectionName, fixture.SearchIndexClient);
        var sut = new AzureAISearchVectorDBCollectionUpdateService(fixture.SearchIndexClient);

        // Act
        await sut.DeleteCollectionAsync(tempCollectionName);

        // Assert
        Assert.False(await sut.CollectionExistsAsync(tempCollectionName));
    }
}
