// Copyright (c) Microsoft. All rights reserved.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Qdrant.Client.Grpc;
using Xunit;
using Xunit.Abstractions;

namespace SemanticKernel.IntegrationTests.Connectors.Memory.Qdrant;

[Collection("QdrantVectorStoreCollection")]
public class QdrantVectorCollectionNonSchemaTests(ITestOutputHelper output, QdrantVectorStoreFixture fixture)
{
    [Fact]
    public async Task ItCanCheckIfCollectionExistsForExistingCollectionAsync()
    {
        // Arrange.
        var sut = new QdrantVectorCollectionNonSchema(fixture.QdrantClient);

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
        var sut = new QdrantVectorCollectionNonSchema(fixture.QdrantClient);

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
        var sut = new QdrantVectorCollectionNonSchema(fixture.QdrantClient);

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

        var sut = new QdrantVectorCollectionNonSchema(fixture.QdrantClient);

        // Act
        await sut.DeleteCollectionAsync(tempCollectionName);

        // Assert
        Assert.False(await sut.CollectionExistsAsync(tempCollectionName));
    }
}
