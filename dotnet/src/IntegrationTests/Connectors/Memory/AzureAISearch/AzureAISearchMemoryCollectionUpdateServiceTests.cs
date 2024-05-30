// Copyright (c) Microsoft. All rights reserved.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Connectors.AzureAISearch;
using Xunit;
using Xunit.Abstractions;

namespace SemanticKernel.IntegrationTests.Connectors.Memory.AzureAISearch;

[Collection("AzureAISearchMemoryCollection")]
public class AzureAISearchMemoryCollectionUpdateServiceTests(ITestOutputHelper output, AzureAISearchMemoryFixture fixture) : IClassFixture<AzureAISearchMemoryFixture>
{
    // If null, all tests will be enabled
    private const string SkipReason = null; //"Requires Azure AI Search Service instance up and running";

    [Fact(Skip = SkipReason)]
    public async Task ItCanCheckIfCollectionExistsForExistingCollectionAsync()
    {
        // Arrange
        var sut = new AzureAISearchMemoryCollectionUpdateService(fixture.SearchIndexClient);

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
        var sut = new AzureAISearchMemoryCollectionUpdateService(fixture.SearchIndexClient);

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
        await AzureAISearchMemoryFixture.DeleteIndexIfExistsAsync(fixture.TestIndexName + "-additional", fixture.SearchIndexClient);
        await AzureAISearchMemoryFixture.CreateIndexAsync(fixture.TestIndexName + "-additional", fixture.SearchIndexClient);
        var sut = new AzureAISearchMemoryCollectionUpdateService(fixture.SearchIndexClient);

        // Act
        var collectionNames = await sut.ListCollectionNamesAsync().ToListAsync();

        // Assert
        Assert.Equal(2, collectionNames.Count);
        Assert.Contains(fixture.TestIndexName, collectionNames);
        Assert.Contains(fixture.TestIndexName + "-additional", collectionNames);

        // Output
        output.WriteLine(string.Join(",", collectionNames));

        // Cleanup
        await AzureAISearchMemoryFixture.DeleteIndexIfExistsAsync(fixture.TestIndexName + "-additional", fixture.SearchIndexClient);
    }

    [Fact(Skip = SkipReason)]
    public async Task ItCanDeleteACollectionAsync()
    {
        // Arrange
        var tempCollectionName = fixture.TestIndexName + "-temp";
        await AzureAISearchMemoryFixture.CreateIndexAsync(tempCollectionName, fixture.SearchIndexClient);
        var sut = new AzureAISearchMemoryCollectionUpdateService(fixture.SearchIndexClient);

        // Act
        await sut.DeleteCollectionAsync(tempCollectionName);

        // Assert
        Assert.False(await sut.CollectionExistsAsync(tempCollectionName));
    }
}
