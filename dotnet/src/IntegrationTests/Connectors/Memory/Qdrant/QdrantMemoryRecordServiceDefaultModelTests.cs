// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel.Memory;
using Xunit;
using Xunit.Abstractions;

namespace SemanticKernel.IntegrationTests.Connectors.Memory.Qdrant;

/// <summary>
/// Contains tests for the <see cref="QdrantMemoryRecordService{TDataModel}"/> class using the default SK data model.
/// </summary>
/// <param name="output">Used for logging.</param>
/// <param name="fixture">Redis setup and teardown.</param>
[Collection("QdrantMemoryCollection")]
public sealed class QdrantMemoryRecordServiceDefaultModelTests(ITestOutputHelper output, QdrantMemoryFixture fixture)
{
    private readonly List<string> _metadataFieldNames = ["HotelName", "hotelCode", "Seafront", "HotelRating", "Tags"];

    private readonly List<string> _stringDataFieldNames = ["Description"];

    [Fact]
    public async Task ItCanGetDocumentFromMemoryStoreAsync()
    {
        // Arrange.
        var mapperOptions = new QdrantMemoryRecordMapperOptions { HasNamedVectors = false, StringDataFieldNames = this._stringDataFieldNames, MetadataFieldNames = this._metadataFieldNames };
        var mapper = new QdrantMemoryRecordMapper(mapperOptions);
        var options = new QdrantMemoryRecordServiceOptions<VectorDBRecord> { HasNamedVectors = false, MapperType = QdrantMemoryRecordMapperType.QdrantPointStructCustomMapper, PointStructCustomMapper = mapper };
        var sut = new QdrantMemoryRecordService<VectorDBRecord>(fixture.QdrantClient, "singleVectorHotels", options);

        // Act.
        var getResult = await sut.GetAsync(11);

        // Assert.
        Assert.Equal(11ul, getResult?.Key);
        Assert.Equal("My Hotel 11", getResult?.Metadata["HotelName"]);
        Assert.Equal(11L, getResult?.Metadata["hotelCode"]);
        Assert.True((bool)getResult?.Metadata["Seafront"]!);
        Assert.Equal(4.5d, getResult?.Metadata["HotelRating"]);
        var tags = getResult?.Metadata["Tags"] as object[];
        Assert.Equal(2, tags?.Length);
        Assert.Equal("t1", tags?[0]);
        Assert.Equal("t2", tags?[1]);
        Assert.Equal("This is a great hotel.", getResult?.StringData["Description"]);
        Assert.Equal(0, getResult?.Vectors.Count);

        // Output.
        output.WriteLine(getResult?.ToString());
    }

    [Fact]
    public async Task ItCanGetDocumentWithGuidIdFromMemoryStoreAsync()
    {
        // Arrange.
        var mapperOptions = new QdrantMemoryRecordMapperOptions { HasNamedVectors = false, StringDataFieldNames = this._stringDataFieldNames, MetadataFieldNames = this._metadataFieldNames };
        var mapper = new QdrantMemoryRecordMapper(mapperOptions);
        var options = new QdrantMemoryRecordServiceOptions<VectorDBRecord> { HasNamedVectors = false, MapperType = QdrantMemoryRecordMapperType.QdrantPointStructCustomMapper, PointStructCustomMapper = mapper };
        var sut = new QdrantMemoryRecordService<VectorDBRecord>(fixture.QdrantClient, "singleVectorGuidIdHotels", options);

        // Act.
        var getResult = await sut.GetAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        // Assert.
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), getResult?.Key);
        Assert.Equal("My Hotel 11", getResult?.Metadata["HotelName"]);
        Assert.Equal("This is a great hotel.", getResult?.StringData["Description"]);
        Assert.Equal(0, getResult?.Vectors.Count);

        // Output.
        output.WriteLine(getResult?.ToString());
    }

    [Fact]
    public async Task ItCanGetDocumentFromMemoryStoreWithEmbeddingsAsync()
    {
        // Arrange.
        var mapperOptions = new QdrantMemoryRecordMapperOptions { HasNamedVectors = false, StringDataFieldNames = this._stringDataFieldNames, MetadataFieldNames = this._metadataFieldNames };
        var mapper = new QdrantMemoryRecordMapper(mapperOptions);
        var options = new QdrantMemoryRecordServiceOptions<VectorDBRecord> { HasNamedVectors = false, MapperType = QdrantMemoryRecordMapperType.QdrantPointStructCustomMapper, PointStructCustomMapper = mapper };
        var sut = new QdrantMemoryRecordService<VectorDBRecord>(fixture.QdrantClient, "singleVectorHotels", options);

        // Act.
        var getResult = await sut.GetAsync(11, new GetRecordOptions { IncludeVectors = true });

        // Assert.
        Assert.Equal(11ul, getResult?.Key);
        Assert.Equal("My Hotel 11", getResult?.Metadata["HotelName"]);
        Assert.Equal(11L, getResult?.Metadata["hotelCode"]);
        Assert.True((bool)getResult?.Metadata["Seafront"]!);
        Assert.Equal(4.5d, getResult?.Metadata["HotelRating"]);
        var tags = getResult?.Metadata["Tags"] as object[];
        Assert.Equal(2, tags?.Length);
        Assert.Equal("t1", tags?[0]);
        Assert.Equal("t2", tags?[1]);
        Assert.Equal("This is a great hotel.", getResult?.StringData["Description"]);
        Assert.NotNull(getResult?.Vectors[string.Empty]);

        // Output.
        output.WriteLine(getResult?.ToString());
    }

    [Fact]
    public async Task ItCanGetDocumentWithNamedVectorsFromMemoryStoreAsync()
    {
        // Arrange.
        var mapperOptions = new QdrantMemoryRecordMapperOptions { HasNamedVectors = true, StringDataFieldNames = this._stringDataFieldNames, MetadataFieldNames = this._metadataFieldNames };
        var mapper = new QdrantMemoryRecordMapper(mapperOptions);
        var options = new QdrantMemoryRecordServiceOptions<VectorDBRecord> { HasNamedVectors = true, MapperType = QdrantMemoryRecordMapperType.QdrantPointStructCustomMapper, PointStructCustomMapper = mapper };
        var sut = new QdrantMemoryRecordService<VectorDBRecord>(fixture.QdrantClient, "namedVectorsHotels", options);

        // Act.
        var getResult = await sut.GetAsync(1);

        // Assert.
        Assert.Equal(1ul, getResult?.Key);
        Assert.Equal("My Hotel 1", getResult?.Metadata["HotelName"]);
        Assert.Equal(1L, getResult?.Metadata["hotelCode"]);
        Assert.True((bool)getResult?.Metadata["Seafront"]!);
        Assert.Equal(4.5d, getResult?.Metadata["HotelRating"]);
        var tags = getResult?.Metadata["Tags"] as object[];
        Assert.Equal(2, tags?.Length);
        Assert.Equal("t1", tags?[0]);
        Assert.Equal("t2", tags?[1]);
        Assert.Equal("This is a great hotel.", getResult?.StringData["Description"]);
        Assert.Equal(0, getResult?.Vectors.Count);

        // Output.
        output.WriteLine(getResult?.ToString());
    }

    [Fact]
    public async Task ItCanGetDocumentWithNamedVectorsFromMemoryStoreWithEmbeddingsAsync()
    {
        // Arrange.
        var mapperOptions = new QdrantMemoryRecordMapperOptions { HasNamedVectors = true, StringDataFieldNames = this._stringDataFieldNames, MetadataFieldNames = this._metadataFieldNames };
        var mapper = new QdrantMemoryRecordMapper(mapperOptions);
        var options = new QdrantMemoryRecordServiceOptions<VectorDBRecord> { HasNamedVectors = true, MapperType = QdrantMemoryRecordMapperType.QdrantPointStructCustomMapper, PointStructCustomMapper = mapper };
        var sut = new QdrantMemoryRecordService<VectorDBRecord>(fixture.QdrantClient, "namedVectorsHotels", options);

        // Act.
        var getResult = await sut.GetAsync(1, new GetRecordOptions { IncludeVectors = true });

        // Assert.
        Assert.Equal(1ul, getResult?.Key);
        Assert.Equal("My Hotel 1", getResult?.Metadata["HotelName"]);
        Assert.Equal(1L, getResult?.Metadata["hotelCode"]);
        Assert.True((bool)getResult?.Metadata["Seafront"]!);
        Assert.Equal(4.5d, getResult?.Metadata["HotelRating"]);
        var tags = getResult?.Metadata["Tags"] as object[];
        Assert.Equal(2, tags?.Length);
        Assert.Equal("t1", tags?[0]);
        Assert.Equal("t2", tags?[1]);
        Assert.Equal("This is a great hotel.", getResult?.StringData["Description"]);
        Assert.NotNull(getResult?.Vectors["DescriptionEmbeddings"]);

        // Output.
        output.WriteLine(getResult?.ToString());
    }

    [Fact]
    public async Task ItCanUpsertDocumentToMemoryStoreAsync()
    {
        // Arrange.
        var mapperOptions = new QdrantMemoryRecordMapperOptions { HasNamedVectors = true, StringDataFieldNames = this._stringDataFieldNames, MetadataFieldNames = this._metadataFieldNames };
        var mapper = new QdrantMemoryRecordMapper(mapperOptions);
        var options = new QdrantMemoryRecordServiceOptions<VectorDBRecord> { HasNamedVectors = true, MapperType = QdrantMemoryRecordMapperType.QdrantPointStructCustomMapper, PointStructCustomMapper = mapper };
        var sut = new QdrantMemoryRecordService<VectorDBRecord>(fixture.QdrantClient, "namedVectorsHotels", options);

        var record = new VectorDBRecord(20ul)
        {
            StringData = new Dictionary<string, string?> { ["Description"] = "This is a great hotel." },
            Metadata = new Dictionary<string, object?> { ["HotelName"] = "My Hotel 20", ["hotelCode"] = 20, ["HotelRating"] = 4.3d, ["Seafront"] = true, ["Tags"] = new List<string> { "t1", "t2" } },
            Vectors = new Dictionary<string, ReadOnlyMemory<object>?> { ["DescriptionEmbeddings"] = new ReadOnlyMemory<object>([30f, 31f, 32f, 33f]) },
        };

        // Act.
        var upsertResult = await sut.UpsertAsync(record);

        // Assert.
        var getResult = await sut.GetAsync(20, new GetRecordOptions { IncludeVectors = true });

        Assert.Equal(20ul, getResult?.Key);
        Assert.Equal("My Hotel 20", getResult?.Metadata["HotelName"]);
        Assert.Equal(20L, getResult?.Metadata["hotelCode"]);
        Assert.True((bool)getResult?.Metadata["Seafront"]!);
        Assert.Equal(4.3d, getResult?.Metadata["HotelRating"]);
        var tags = getResult?.Metadata["Tags"] as object[];
        Assert.Equal(2, tags?.Length);
        Assert.Equal("t1", tags?[0]);
        Assert.Equal("t2", tags?[1]);
        Assert.Equal("This is a great hotel.", getResult?.StringData["Description"]);
        Assert.NotNull(getResult?.Vectors["DescriptionEmbeddings"]);

        // Output.
        output.WriteLine(upsertResult.ToString(CultureInfo.InvariantCulture));
        output.WriteLine(getResult?.ToString());
    }
}
