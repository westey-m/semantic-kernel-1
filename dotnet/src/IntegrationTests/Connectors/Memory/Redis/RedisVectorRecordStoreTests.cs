// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Redis;
using Microsoft.SemanticKernel.Memory;
using Xunit;
using Xunit.Abstractions;
using static SemanticKernel.IntegrationTests.Connectors.Memory.Redis.RedisVectorStoreFixture;

namespace SemanticKernel.IntegrationTests.Connectors.Memory.Redis;

/// <summary>
/// Contains tests for the <see cref="RedisVectorRecordStore{TRecord}"/> class.
/// </summary>
/// <param name="output">Used for logging.</param>
/// <param name="fixture">Redis setup and teardown.</param>
[Collection("RedisVectorStoreCollection")]
public sealed class RedisVectorRecordStoreTests(ITestOutputHelper output, RedisVectorStoreFixture fixture)
{
    [Fact]
    public async Task ItCanGetDocumentFromVectorStoreAsync()
    {
        // Arrange.
        var options = new RedisVectorRecordStoreOptions<HotelInfo> { DefaultCollectionName = "hotels", PrefixCollectionNameToKeyNames = true };
        var sut = new RedisVectorRecordStore<HotelInfo>(fixture.Database, options);

        // Act.
        var getResult = await sut.GetAsync("H10");

        // Assert.
        Assert.Equal("H10", getResult?.HotelId);
        Assert.Equal("My Hotel 10", getResult?.HotelName);
        Assert.Equal(10, getResult?.HotelCode);
        Assert.True(getResult?.Seafront);
        Assert.Equal("Seattle", getResult?.Address.City);
        Assert.Equal("This is a great hotel.", getResult?.Description);
        Assert.Null(getResult?.DescriptionEmbeddings);

        // Output.
        output.WriteLine(getResult?.ToString());
    }

    [Fact]
    public async Task ItCanGetDocumentFromVectorStoreWithEmbeddingsAsync()
    {
        // Arrange.
        var options = new RedisVectorRecordStoreOptions<HotelInfo> { DefaultCollectionName = "hotels", PrefixCollectionNameToKeyNames = true };
        var sut = new RedisVectorRecordStore<HotelInfo>(fixture.Database, options);

        // Act.
        var getResult = await sut.GetAsync("H10", new GetRecordOptions { IncludeVectors = true });

        // Assert.
        Assert.Equal("H10", getResult?.HotelId);
        Assert.Equal("My Hotel 10", getResult?.HotelName);
        Assert.Equal(10, getResult?.HotelCode);
        Assert.True(getResult?.Seafront);
        Assert.Equal("Seattle", getResult?.Address.City);
        Assert.Equal("This is a great hotel.", getResult?.Description);
        Assert.NotNull(getResult?.DescriptionEmbeddings);

        // Output.
        output.WriteLine(getResult?.ToString());
    }

    [Fact]
    public async Task ItFailsToGetDocumentsWithInvalidSchemaAsync()
    {
        // Arrange.
        var options = new RedisVectorRecordStoreOptions<HotelInfo> { DefaultCollectionName = "hotels", PrefixCollectionNameToKeyNames = true };
        var sut = new RedisVectorRecordStore<HotelInfo>(fixture.Database, options);

        // Act & Assert.
        await Assert.ThrowsAsync<HttpOperationException>(async () => await sut.GetAsync("H13-Invalid", new GetRecordOptions { IncludeVectors = true }));
    }

    [Fact]
    public async Task ItCanGetManyDocumentsFromVectorStoreAsync()
    {
        // Arrange
        var options = new RedisVectorRecordStoreOptions<HotelInfo> { DefaultCollectionName = "hotels", PrefixCollectionNameToKeyNames = true };
        var sut = new RedisVectorRecordStore<HotelInfo>(fixture.Database, options);

        // Act
        var hotels = sut.GetBatchAsync(["H10", "H11"], new GetRecordOptions { IncludeVectors = true });

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
        var options = new RedisVectorRecordStoreOptions<HotelInfo> { DefaultCollectionName = "hotels", PrefixCollectionNameToKeyNames = true };
        var sut = new RedisVectorRecordStore<HotelInfo>(fixture.Database, options);

        // Act & Assert.
        await Assert.ThrowsAsync<HttpOperationException>(async () => await sut.GetBatchAsync(["H10", "H15", "H11"], new GetRecordOptions { IncludeVectors = true }).ToListAsync());
    }

    [Fact]
    public async Task ItCanRemoveDocumentFromVectorStoreAsync()
    {
        // Arrange.
        var options = new RedisVectorRecordStoreOptions<HotelInfo> { DefaultCollectionName = "hotels", PrefixCollectionNameToKeyNames = true };
        var sut = new RedisVectorRecordStore<HotelInfo>(fixture.Database, options);
        var address = new HotelAddress("Seattle", "USA");
        var record = new HotelInfo("TMP20", "My Hotel 20", 20, true, address, "This is a great hotel.", Array.Empty<float>());
        await sut.UpsertAsync(record);

        // Act.
        await sut.DeleteAsync("TMP20");

        // Assert.
        await Assert.ThrowsAsync<HttpOperationException>(async () => await sut.GetAsync("TMP20"));
    }

    [Fact]
    public async Task ItCanUpsertDocumentToVectorStoreAsync()
    {
        // Arrange.
        var options = new RedisVectorRecordStoreOptions<HotelInfo> { DefaultCollectionName = "hotels", PrefixCollectionNameToKeyNames = true };
        var sut = new RedisVectorRecordStore<HotelInfo>(fixture.Database, options);
        var address = new HotelAddress("Seattle", "USA");
        var record = new HotelInfo("H1", "My Hotel 1", 1, true, address, "This is a great hotel.", new[] { 30f, 31f, 32f, 33f });

        // Act.
        var upsertResult = await sut.UpsertAsync(record);

        // Assert.
        var getResult = await sut.GetAsync("H1", new GetRecordOptions { IncludeVectors = true });
        Assert.Equal("H1", upsertResult);
        Assert.Equal(record.HotelId, getResult?.HotelId);
        Assert.Equal(record.HotelName, getResult?.HotelName);
        Assert.Equal(record.HotelCode, getResult?.HotelCode);
        Assert.Equal(record.Seafront, getResult?.Seafront);
        Assert.Equal(record.Address, getResult?.Address);
        Assert.Equal(record.Description, getResult?.Description);
        Assert.Equal(record.DescriptionEmbeddings?.ToArray(), getResult?.DescriptionEmbeddings?.ToArray());

        // Output.
        output.WriteLine(upsertResult);
        output.WriteLine(getResult?.ToString());
    }
}
