// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel.Memory;
using Xunit;
using Xunit.Abstractions;

namespace SemanticKernel.IntegrationTests.Connectors.Memory.Qdrant;

/// <summary>
/// Contains tests for the <see cref="QdrantVectorStore{TDataModel}"/> class.
/// </summary>
/// <param name="output">Used for logging.</param>
/// <param name="fixture">Redis setup and teardown.</param>
[Collection("QdrantVectorStoreCollection")]
public sealed class QdrantVectorStoreTests(ITestOutputHelper output, QdrantVectorStoreFixture fixture)
{
    [Fact]
    public async Task ItCanGetDocumentFromVectorStoreAsync()
    {
        // Arrange.
        var sut = new QdrantVectorStore<QdrantVectorStoreFixture.HotelShortInfo>(fixture.QdrantClient, "singleVectorHotels", new QdrantVectorStoreOptions { IdType = QdrantVectorStoreOptions.QdrantIdType.Ulong });

        // Act.
        var getResult = await sut.GetAsync("11");

        // Assert.
        Assert.Equal("11", getResult?.HotelId);

        // Output.
        output.WriteLine(getResult?.ToString());
    }

    [Fact]
    public async Task ItCanGetDocumentFromVectorStoreWithEmbeddingsAsync()
    {
        // Arrange.
        var sut = new QdrantVectorStore<QdrantVectorStoreFixture.HotelShortInfo>(fixture.QdrantClient, "singleVectorHotels", new QdrantVectorStoreOptions { IdType = QdrantVectorStoreOptions.QdrantIdType.Ulong });

        // Act.
        var getResult = await sut.GetAsync("11", new VectorStoreGetDocumentOptions { IncludeEmbeddings = true });

        // Assert.
        Assert.Equal("11", getResult?.HotelId);

        // Output.
        output.WriteLine(getResult?.ToString());
    }

    [Fact]
    public async Task ItCanGetDocumentWithNamedVectorsFromVectorStoreAsync()
    {
        // Arrange.
        var sut = new QdrantVectorStore<QdrantVectorStoreFixture.HotelShortInfo>(fixture.QdrantClient, "namedVectorsHotels", new QdrantVectorStoreOptions { IdType = QdrantVectorStoreOptions.QdrantIdType.Ulong, HasNamedVectors = true });

        // Act.
        var getResult = await sut.GetAsync("1");

        // Assert.
        Assert.Equal("1", getResult?.HotelId);

        // Output.
        output.WriteLine(getResult?.ToString());
    }

    [Fact]
    public async Task ItCanGetDocumentWithNamedVectorsFromVectorStoreWithEmbeddingsAsync()
    {
        // Arrange.
        var sut = new QdrantVectorStore<QdrantVectorStoreFixture.HotelShortInfo>(fixture.QdrantClient, "namedVectorsHotels", new QdrantVectorStoreOptions { IdType = QdrantVectorStoreOptions.QdrantIdType.Ulong, HasNamedVectors = true });

        // Act.
        var getResult = await sut.GetAsync("1", new VectorStoreGetDocumentOptions { IncludeEmbeddings = true });

        // Assert.
        Assert.Equal("1", getResult?.HotelId);

        // Output.
        output.WriteLine(getResult?.ToString());
    }
}
