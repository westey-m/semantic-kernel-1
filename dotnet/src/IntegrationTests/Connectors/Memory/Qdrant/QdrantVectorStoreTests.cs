// Copyright (c) Microsoft. All rights reserved.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
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
        var sut = new QdrantVectorStore<QdrantVectorStoreFixture.HotelInfo>(fixture.QdrantClient, "singleVectorHotels", new QdrantVectorStoreOptions { IdType = QdrantVectorStoreOptions.QdrantIdType.Ulong });

        // Act.
        var getResult = await sut.GetAsync("11");

        // Assert.
        Assert.Equal("11", getResult?.HotelId);
        Assert.Equal("My Hotel 11", getResult?.HotelName);
        Assert.Equal(11, getResult?.HotelCode);
        Assert.Equal("This is a great hotel.", getResult?.Description);
        Assert.Null(getResult?.DescriptionEmbeddings);

        // Output.
        output.WriteLine(getResult?.ToString());
    }

    [Fact]
    public async Task ItCanGetDocumentFromVectorStoreWithEmbeddingsAsync()
    {
        // Arrange.
        var sut = new QdrantVectorStore<QdrantVectorStoreFixture.HotelInfo>(fixture.QdrantClient, "singleVectorHotels", new QdrantVectorStoreOptions { IdType = QdrantVectorStoreOptions.QdrantIdType.Ulong });

        // Act.
        var getResult = await sut.GetAsync("11", new VectorStoreGetDocumentOptions { IncludeEmbeddings = true });

        // Assert.
        Assert.Equal("11", getResult?.HotelId);
        Assert.Equal("My Hotel 11", getResult?.HotelName);
        Assert.Equal(11, getResult?.HotelCode);
        Assert.Equal("This is a great hotel.", getResult?.Description);
        Assert.NotNull(getResult?.DescriptionEmbeddings);

        // Output.
        output.WriteLine(getResult?.ToString());
    }

    [Fact]
    public async Task ItCanGetDocumentWithNamedVectorsFromVectorStoreAsync()
    {
        // Arrange.
        var sut = new QdrantVectorStore<QdrantVectorStoreFixture.HotelInfo>(fixture.QdrantClient, "namedVectorsHotels", new QdrantVectorStoreOptions { IdType = QdrantVectorStoreOptions.QdrantIdType.Ulong, HasNamedVectors = true });

        // Act.
        var getResult = await sut.GetAsync("1");

        // Assert.
        Assert.Equal("1", getResult?.HotelId);
        Assert.Equal("My Hotel 1", getResult?.HotelName);
        Assert.Equal(1, getResult?.HotelCode);
        Assert.Equal("This is a great hotel.", getResult?.Description);
        Assert.Null(getResult?.DescriptionEmbeddings);

        // Output.
        output.WriteLine(getResult?.ToString());
    }

    [Fact]
    public async Task ItCanGetDocumentWithNamedVectorsFromVectorStoreWithEmbeddingsAsync()
    {
        // Arrange.
        var sut = new QdrantVectorStore<QdrantVectorStoreFixture.HotelInfo>(fixture.QdrantClient, "namedVectorsHotels", new QdrantVectorStoreOptions { IdType = QdrantVectorStoreOptions.QdrantIdType.Ulong, HasNamedVectors = true });

        // Act.
        var getResult = await sut.GetAsync("1", new VectorStoreGetDocumentOptions { IncludeEmbeddings = true });

        // Assert.
        Assert.Equal("1", getResult?.HotelId);
        Assert.Equal("My Hotel 1", getResult?.HotelName);
        Assert.Equal(1, getResult?.HotelCode);
        Assert.Equal("This is a great hotel.", getResult?.Description);
        Assert.NotNull(getResult?.DescriptionEmbeddings);

        // Output.
        output.WriteLine(getResult?.ToString());
    }

    [Fact]
    public async Task ItCanRemoveDocumentFromVectorStoreAsync()
    {
        // Arrange.
        var sut = new QdrantVectorStore<QdrantVectorStoreFixture.HotelInfo>(fixture.QdrantClient, "namedVectorsHotels", new QdrantVectorStoreOptions { IdType = QdrantVectorStoreOptions.QdrantIdType.Ulong, HasNamedVectors = true });
        var record = new QdrantVectorStoreFixture.HotelInfo
        {
            HotelId = "20",
            HotelName = "My Hotel 20",
            HotelCode = 20,
            Seafront = true,
            Description = "This is a great hotel.",
            DescriptionEmbeddings = Enumerable.Range(1, 100).Select(x => (float)x).ToArray(),
        };
        await sut.UpsertAsync(record);

        // Act.
        var removeResult = await sut.RemoveAsync("20");

        // Assert.
        Assert.Equal("20", removeResult);
        await Assert.ThrowsAsync<HttpOperationException>(async () => await sut.GetAsync("20"));

        // Output.
        output.WriteLine(removeResult);
    }

    [Fact]
    public async Task ItCanUpsertDocumentToVectorStoreAsync()
    {
        // Arrange.
        var sut = new QdrantVectorStore<QdrantVectorStoreFixture.HotelInfo>(fixture.QdrantClient, "namedVectorsHotels", new QdrantVectorStoreOptions { IdType = QdrantVectorStoreOptions.QdrantIdType.Ulong, HasNamedVectors = true });
        var record = new QdrantVectorStoreFixture.HotelInfo
        {
            HotelId = "20",
            HotelName = "My Hotel 20",
            HotelCode = 20,
            Seafront = true,
            Description = "This is a great hotel.",
            DescriptionEmbeddings = new[] { 30f, 31f, 32f, 33f },
        };

        // Act.
        var upsertResult = await sut.UpsertAsync(record);

        // Assert.
        var getResult = await sut.GetAsync("20", new VectorStoreGetDocumentOptions { IncludeEmbeddings = true });
        Assert.Equal("20", upsertResult);
        Assert.Equal(record.HotelId, getResult?.HotelId);
        Assert.Equal(record.HotelName, getResult?.HotelName);
        Assert.Equal(record.HotelCode, getResult?.HotelCode);
        Assert.Equal(record.Seafront, getResult?.Seafront);
        Assert.Equal(record.Description, getResult?.Description);
        Assert.Equal(record.DescriptionEmbeddings?.ToArray(), getResult?.DescriptionEmbeddings?.ToArray());

        // Output.
        output.WriteLine(upsertResult);
        output.WriteLine(getResult?.ToString());
    }
}
