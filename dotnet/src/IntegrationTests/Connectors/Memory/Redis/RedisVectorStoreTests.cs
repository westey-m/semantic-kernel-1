// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Redis;
using Microsoft.SemanticKernel.Memory;
using Xunit;
using Xunit.Abstractions;

namespace SemanticKernel.IntegrationTests.Connectors.Memory.Redis;

/// <summary>
/// Contains tests for the <see cref="RedisVectorStore{TDataModel}"/> class.
/// </summary>
/// <param name="output">Used for logging.</param>
/// <param name="fixture">Redis setup and teardown.</param>
[Collection("RedisVectorStoreCollection")]
public sealed class RedisVectorStoreTests(ITestOutputHelper output, RedisVectorStoreFixture fixture)
{
    [Fact]
    public async Task ItCanGetDocumentFromVectorStoreAsync()
    {
        // Arrange.
        var sut = new RedisVectorStore<RedisVectorStoreFixture.HotelInfo>(fixture.Database, "hotels", new RedisVectorStoreOptions { PrefixCollectionNameToKeyNames = true });

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
        var sut = new RedisVectorStore<RedisVectorStoreFixture.HotelInfo>(fixture.Database, "hotels", new RedisVectorStoreOptions { PrefixCollectionNameToKeyNames = true });

        // Act.
        var getResult = await sut.GetAsync("H10", new VectorStoreGetDocumentOptions { IncludeEmbeddings = true });

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
        var sut = new RedisVectorStore<RedisVectorStoreFixture.HotelInfo>(fixture.Database, "hotels", new RedisVectorStoreOptions { PrefixCollectionNameToKeyNames = true });

        // Act & Assert.
        await Assert.ThrowsAsync<HttpOperationException>(async () => await sut.GetAsync("H13-Invalid", new VectorStoreGetDocumentOptions { IncludeEmbeddings = true }));
    }

    [Fact]
    public async Task ItCanRemoveDocumentFromVectorStoreAsync()
    {
        // Arrange.
        var sut = new RedisVectorStore<RedisVectorStoreFixture.HotelInfo>(fixture.Database, "hotels", new RedisVectorStoreOptions { PrefixCollectionNameToKeyNames = true });
        var address = new RedisVectorStoreFixture.HotelAddress("Seattle", "USA");
        var record = new RedisVectorStoreFixture.HotelInfo("TMP20", "My Hotel 20", 20, true, address, "This is a great hotel.", Array.Empty<float>());
        await sut.UpsertAsync(record);

        // Act.
        var removeResult = await sut.RemoveAsync("TMP20");

        // Assert.
        Assert.Equal("TMP20", removeResult);
        await Assert.ThrowsAsync<HttpOperationException>(async () => await sut.GetAsync("TMP20"));

        // Output.
        output.WriteLine(removeResult);
    }

    [Fact]
    public async Task ItCanUpsertDocumentToVectorStoreAsync()
    {
        // Arrange.
        var sut = new RedisVectorStore<RedisVectorStoreFixture.HotelInfo>(fixture.Database, "hotels", new RedisVectorStoreOptions { PrefixCollectionNameToKeyNames = true });
        var address = new RedisVectorStoreFixture.HotelAddress("Seattle", "USA");
        var record = new RedisVectorStoreFixture.HotelInfo("H1", "My Hotel 1", 1, true, address, "This is a great hotel.", new[] { 30f, 31f, 32f, 33f });

        // Act.
        var upsertResult = await sut.UpsertAsync(record);

        // Assert.
        var getResult = await sut.GetAsync("H1", new VectorStoreGetDocumentOptions { IncludeEmbeddings = true });
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
