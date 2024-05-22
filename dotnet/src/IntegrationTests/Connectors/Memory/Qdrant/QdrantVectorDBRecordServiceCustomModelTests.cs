// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel.Memory;
using Xunit;
using Xunit.Abstractions;
using static SemanticKernel.IntegrationTests.Connectors.Memory.Qdrant.QdrantVectorDBRecordServiceFixture;

namespace SemanticKernel.IntegrationTests.Connectors.Memory.Qdrant;

/// <summary>
/// Contains tests for the <see cref="QdrantVectorDBRecordService{TDataModel}"/> class using a custom data model.
/// </summary>
/// <param name="output">Used for logging.</param>
/// <param name="fixture">Redis setup and teardown.</param>
[Collection("QdrantVectorDBRecordServiceCollection")]
public sealed class QdrantVectorDBRecordServiceCustomModelTests(ITestOutputHelper output, QdrantVectorDBRecordServiceFixture fixture)
{
    [Fact]
    public async Task ItCanGetDocumentFromVectorStoreAsync()
    {
        // Arrange.
        var sut = new QdrantVectorDBRecordService<HotelInfo>(fixture.QdrantClient, "singleVectorHotels", new QdrantVectorDBRecordServiceOptions<HotelInfo> { HasNamedVectors = false });

        // Act.
        var getResult = await sut.GetAsync(11);

        // Assert.
        Assert.Equal(11ul, getResult?.HotelId);
        Assert.Equal("My Hotel 11", getResult?.HotelName);
        Assert.Equal(11, getResult?.HotelCode);
        Assert.True(getResult?.Seafront);
        Assert.Equal(4.5f, getResult?.HotelRating);
        Assert.Equal(2, getResult?.Tags.Count);
        Assert.Equal("t1", getResult?.Tags[0]);
        Assert.Equal("t2", getResult?.Tags[1]);
        Assert.Equal("This is a great hotel.", getResult?.Description);
        Assert.Null(getResult?.DescriptionEmbeddings);

        // Output.
        output.WriteLine(getResult?.ToString());
    }

    [Fact]
    public async Task ItCanGetDocumentWithGuidIdFromVectorStoreAsync()
    {
        // Arrange.
        var sut = new QdrantVectorDBRecordService<HotelInfoWithGuidId>(fixture.QdrantClient, "singleVectorGuidIdHotels", new QdrantVectorDBRecordServiceOptions<HotelInfoWithGuidId> { HasNamedVectors = false });

        // Act.
        var getResult = await sut.GetAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        // Assert.
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), getResult?.HotelId);
        Assert.Equal("My Hotel 11", getResult?.HotelName);
        Assert.Equal("This is a great hotel.", getResult?.Description);
        Assert.Null(getResult?.DescriptionEmbeddings);

        // Output.
        output.WriteLine(getResult?.ToString());
    }

    [Fact]
    public async Task ItCanGetDocumentFromVectorStoreWithEmbeddingsAsync()
    {
        // Arrange.
        var sut = new QdrantVectorDBRecordService<HotelInfo>(fixture.QdrantClient, "singleVectorHotels", new QdrantVectorDBRecordServiceOptions<HotelInfo> { HasNamedVectors = false });

        // Act.
        var getResult = await sut.GetAsync(11, new GetRecordOptions { IncludeVectors = true });

        // Assert.
        Assert.Equal(11ul, getResult?.HotelId);
        Assert.Equal("My Hotel 11", getResult?.HotelName);
        Assert.Equal(11, getResult?.HotelCode);
        Assert.True(getResult?.Seafront);
        Assert.Equal(4.5f, getResult?.HotelRating);
        Assert.Equal(2, getResult?.Tags.Count);
        Assert.Equal("t1", getResult?.Tags[0]);
        Assert.Equal("t2", getResult?.Tags[1]);
        Assert.Equal("This is a great hotel.", getResult?.Description);
        Assert.NotNull(getResult?.DescriptionEmbeddings);

        // Output.
        output.WriteLine(getResult?.ToString());
    }

    [Fact]
    public async Task ItCanGetDocumentWithNamedVectorsFromVectorStoreAsync()
    {
        // Arrange.
        var sut = new QdrantVectorDBRecordService<HotelInfo>(fixture.QdrantClient, "namedVectorsHotels", new QdrantVectorDBRecordServiceOptions<HotelInfo> { HasNamedVectors = true });

        // Act.
        var getResult = await sut.GetAsync(1);

        // Assert.
        Assert.Equal(1ul, getResult?.HotelId);
        Assert.Equal("My Hotel 1", getResult?.HotelName);
        Assert.Equal(1, getResult?.HotelCode);
        Assert.True(getResult?.Seafront);
        Assert.Equal(4.5f, getResult?.HotelRating);
        Assert.Equal(2, getResult?.Tags.Count);
        Assert.Equal("t1", getResult?.Tags[0]);
        Assert.Equal("t2", getResult?.Tags[1]);
        Assert.Equal("This is a great hotel.", getResult?.Description);
        Assert.Null(getResult?.DescriptionEmbeddings);

        // Output.
        output.WriteLine(getResult?.ToString());
    }

    [Fact]
    public async Task ItCanGetDocumentWithNamedVectorsFromVectorStoreWithEmbeddingsAsync()
    {
        // Arrange.
        var sut = new QdrantVectorDBRecordService<HotelInfo>(fixture.QdrantClient, "namedVectorsHotels", new QdrantVectorDBRecordServiceOptions<HotelInfo> { HasNamedVectors = true });

        // Act.
        var getResult = await sut.GetAsync(1, new GetRecordOptions { IncludeVectors = true });

        // Assert.
        Assert.Equal(1ul, getResult?.HotelId);
        Assert.Equal("My Hotel 1", getResult?.HotelName);
        Assert.Equal(1, getResult?.HotelCode);
        Assert.True(getResult?.Seafront);
        Assert.Equal(4.5f, getResult?.HotelRating);
        Assert.Equal(2, getResult?.Tags.Count);
        Assert.Equal("t1", getResult?.Tags[0]);
        Assert.Equal("t2", getResult?.Tags[1]);
        Assert.Equal("This is a great hotel.", getResult?.Description);
        Assert.NotNull(getResult?.DescriptionEmbeddings);

        // Output.
        output.WriteLine(getResult?.ToString());
    }

    [Fact]
    public async Task ItCanGetManyDocumentsFromVectorStoreAsync()
    {
        // Arrange
        var sut = new QdrantVectorDBRecordService<HotelInfo>(fixture.QdrantClient, "namedVectorsHotels", new QdrantVectorDBRecordServiceOptions<HotelInfo> { HasNamedVectors = true });

        // Act
        var hotels = sut.GetBatchAsync([1, 2], new GetRecordOptions { IncludeVectors = true });

        // Assert
        Assert.NotNull(hotels);
        var hotelsList = await hotels.ToListAsync();
        Assert.Equal(2, hotelsList.Count);

        // Output
        foreach (var hotel in hotelsList)
        {
            output.WriteLine(hotel?.ToString() ?? "Null");
        }
    }

    [Fact]
    public async Task ItThrowsForPartialBatchResultAsync()
    {
        // Arrange.
        var sut = new QdrantVectorDBRecordService<HotelInfo>(fixture.QdrantClient, "namedVectorsHotels", new QdrantVectorDBRecordServiceOptions<HotelInfo> { HasNamedVectors = true });

        // Act.
        await Assert.ThrowsAsync<HttpOperationException>(async () => await sut.GetBatchAsync([1, 5, 2]).ToListAsync());
    }

    [Fact]
    public async Task ItCanRemoveDocumentFromVectorStoreAsync()
    {
        // Arrange.
        var sut = new QdrantVectorDBRecordService<HotelInfo>(fixture.QdrantClient, "namedVectorsHotels", new QdrantVectorDBRecordServiceOptions<HotelInfo> { HasNamedVectors = true });

        var record = new HotelInfo
        {
            HotelId = 20,
            HotelName = "My Hotel 20",
            HotelCode = 20,
            Seafront = true,
            Description = "This is a great hotel.",
            DescriptionEmbeddings = new[] { 30f, 31f, 32f, 33f },
        };
        await sut.UpsertAsync(record);

        // Act.
        await sut.RemoveAsync(20);

        // Assert.
        await Assert.ThrowsAsync<HttpOperationException>(async () => await sut.GetAsync(20));
    }

    [Fact]
    public async Task ItCanUpsertDocumentToVectorStoreAsync()
    {
        // Arrange.
        var sut = new QdrantVectorDBRecordService<HotelInfo>(fixture.QdrantClient, "namedVectorsHotels", new QdrantVectorDBRecordServiceOptions<HotelInfo> { HasNamedVectors = true });

        var record = new HotelInfo
        {
            HotelId = 20,
            HotelName = "My Hotel 20",
            HotelCode = 20,
            HotelRating = 4.3f,
            Seafront = true,
            Tags = { "t1", "t2" },
            Description = "This is a great hotel.",
            DescriptionEmbeddings = new[] { 30f, 31f, 32f, 33f },
        };

        // Act.
        var upsertResult = await sut.UpsertAsync(record);

        // Assert.
        var getResult = await sut.GetAsync(20, new GetRecordOptions { IncludeVectors = true });
        Assert.Equal(20ul, upsertResult);
        Assert.Equal(record.HotelId, getResult?.HotelId);
        Assert.Equal(record.HotelName, getResult?.HotelName);
        Assert.Equal(record.HotelCode, getResult?.HotelCode);
        Assert.Equal(record.HotelRating, getResult?.HotelRating);
        Assert.Equal(record.Seafront, getResult?.Seafront);
        Assert.Equal(record.Tags.ToArray(), getResult?.Tags.ToArray());
        Assert.Equal(record.Description, getResult?.Description);

        // TODO: figure out why original array is different from the one we get back.
        //Assert.Equal(record.DescriptionEmbeddings?.ToArray(), getResult?.DescriptionEmbeddings?.ToArray());

        // Output.
        output.WriteLine(upsertResult.ToString(CultureInfo.InvariantCulture));
        output.WriteLine(getResult?.ToString());
    }

    [Fact]
    public async Task ItCanUpsertAndRemoveDocumentWithGuidIdToVectorStoreAsync()
    {
        // Arrange.
        IVectorDBRecordService<Guid, HotelInfoWithGuidId> sut = new QdrantVectorDBRecordService<HotelInfoWithGuidId>(fixture.QdrantClient, "singleVectorGuidIdHotels", new QdrantVectorDBRecordServiceOptions<HotelInfoWithGuidId> { HasNamedVectors = false });

        var record = new HotelInfoWithGuidId
        {
            HotelId = Guid.Parse("55555555-5555-5555-5555-555555555555"),
            HotelName = "My Hotel 5",
            Description = "This is a great hotel.",
            DescriptionEmbeddings = new[] { 30f, 31f, 32f, 33f },
        };

        // Act.
        var upsertResult = await sut.UpsertAsync(record);

        // Assert.
        var getResult = await sut.GetAsync(Guid.Parse("55555555-5555-5555-5555-555555555555"), new GetRecordOptions { IncludeVectors = true });
        Assert.Equal(Guid.Parse("55555555-5555-5555-5555-555555555555"), upsertResult);
        Assert.Equal(record.HotelId, getResult?.HotelId);
        Assert.Equal(record.HotelName, getResult?.HotelName);
        Assert.Equal(record.Description, getResult?.Description);

        // Act.
        await sut.RemoveAsync(Guid.Parse("55555555-5555-5555-5555-555555555555"));

        // Assert.
        await Assert.ThrowsAsync<HttpOperationException>(async () => await sut.GetAsync(Guid.Parse("55555555-5555-5555-5555-555555555555")));

        // Output.
        output.WriteLine(upsertResult.ToString("D"));
        output.WriteLine(getResult?.ToString());
    }
}
