// Copyright (c) Microsoft. All rights reserved.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Xunit;
using Xunit.Abstractions;
using static SemanticKernel.IntegrationTests.Connectors.Memory.Qdrant.QdrantVectorStoreFixture;

namespace SemanticKernel.IntegrationTests.Connectors.Memory.Qdrant;

[Collection("QdrantVectorStoreCollection")]
public class QdrantVectorStoreTests(ITestOutputHelper output, QdrantVectorStoreFixture fixture)
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task ItCanCreateACollectionAsync(bool useDefinition, bool hasNamedVectors)
    {
        // Arrange
        var collectionNamePostfix1 = useDefinition ? "WithDefinition" : "WithType";
        var collectionNamePostfix2 = hasNamedVectors ? "HasNamedVectors" : "SingleUnnamedVector";
        var testCollectionName = $"createtest{collectionNamePostfix1}{collectionNamePostfix2}";
        var options = new QdrantVectorStoreOptions { HasNamedVectors = hasNamedVectors };
        var sut = new QdrantVectorStore(
            fixture.QdrantClient,
            options);
        var collection = sut.GetCollection<ulong, HotelInfo>(testCollectionName);

        // Act
        await sut.CreateCollectionAsync<ulong, HotelInfo>(testCollectionName, useDefinition ? fixture.HotelVectorStoreRecordDefinition : null);

        // Assert
        var existResult = await collection.CollectionExistsAsync();
        Assert.True(existResult);
        await collection.DeleteCollectionAsync();

        // Output
        output.WriteLine(existResult.ToString());
    }

    [Fact]
    public async Task ItCanGetAListOfExistingCollectionNamesAsync()
    {
        // Arrange
        var sut = new QdrantVectorStore(fixture.QdrantClient);

        // Act
        var collectionNames = await sut.ListCollectionNamesAsync().ToListAsync();

        // Assert
        Assert.Equal(3, collectionNames.Count);
        Assert.Contains("namedVectorsHotels", collectionNames);
        Assert.Contains("singleVectorHotels", collectionNames);
        Assert.Contains("singleVectorGuidIdHotels", collectionNames);

        // Output
        output.WriteLine(string.Join(",", collectionNames));
    }
}
