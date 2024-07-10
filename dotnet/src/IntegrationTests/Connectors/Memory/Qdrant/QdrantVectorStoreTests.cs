// Copyright (c) Microsoft. All rights reserved.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Qdrant.Client.Grpc;
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

        // Act
        await sut.CreateCollectionAsync<ulong, HotelInfo>(testCollectionName, useDefinition ? fixture.HotelVectorStoreRecordDefinition : null);

        // Assert
        var existResult = await sut.CollectionExistsAsync(testCollectionName);
        Assert.True(existResult);
        await sut.DeleteCollectionAsync(testCollectionName);

        // Output
        output.WriteLine(existResult.ToString());
    }

    [Fact]
    public async Task ItCanCheckIfCollectionExistsForExistingCollectionAsync()
    {
        // Arrange.
        var sut = new QdrantVectorStore(fixture.QdrantClient);

        // Act.
        var doesExistResult = await sut.CollectionExistsAsync("namedVectorsHotels");

        // Assert.
        Assert.True(doesExistResult);

        // Output.
        output.WriteLine(doesExistResult.ToString());
    }

    [Fact]
    public async Task ItCanCheckIfCollectionExistsForNonExistingCollectionAsync()
    {
        // Arrange.
        var sut = new QdrantVectorStore(fixture.QdrantClient);

        // Act.
        var doesExistResult = await sut.CollectionExistsAsync("non-existing-collection");

        // Assert.
        Assert.False(doesExistResult);

        // Output.
        output.WriteLine(doesExistResult.ToString());
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

    [Fact]
    public async Task ItCanDeleteACollectionAsync()
    {
        // Arrange
        var tempCollectionName = "temp-test";
        await fixture.QdrantClient.CreateCollectionAsync(
            tempCollectionName,
            new VectorParams { Size = 4, Distance = Distance.Cosine });

        var sut = new QdrantVectorStore(fixture.QdrantClient);

        // Act
        await sut.DeleteCollectionAsync(tempCollectionName);

        // Assert
        Assert.False(await sut.CollectionExistsAsync(tempCollectionName));
    }
}
