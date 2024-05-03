// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Redis;
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
        var sut = new RedisVectorStore<RedisVectorStoreFixture.HotelShortInfo>(fixture.Database, "hotels", new RedisVectorStoreOptions { PrefixCollectionNameToKeyNames = true });

        // Act.
        var getResult = await sut.GetAsync("H10");

        // Assert.
        Assert.Equal("H10", getResult?.HotelId);

        // Output.
        output.WriteLine(getResult?.ToString());
    }

    [Fact]
    public async Task ItFailsToGetDocumentsWithInvalidSchemaAsync()
    {
        // Arrange.
        var sut = new RedisVectorStore<RedisVectorStoreFixture.HotelShortInfo>(fixture.Database, "hotels", new RedisVectorStoreOptions { PrefixCollectionNameToKeyNames = true });

        // Act & Assert.
        await Assert.ThrowsAsync<HttpOperationException>(async () => await sut.GetAsync("H13-Invalid"));
    }

    [Fact]
    public async Task ItCanRemoveDocumentFromVectorStoreAsync()
    {
        // Arrange.
        var sut = new RedisVectorStore<RedisVectorStoreFixture.HotelShortInfo>(fixture.Database, "hotels", new RedisVectorStoreOptions { PrefixCollectionNameToKeyNames = true });
        var record = new RedisVectorStoreFixture.HotelShortInfo("TMP20", "My Hotel 20", "This is a great hotel.", Array.Empty<float>());
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
        var sut = new RedisVectorStore<RedisVectorStoreFixture.HotelShortInfo>(fixture.Database, "hotels", new RedisVectorStoreOptions { PrefixCollectionNameToKeyNames = true });
        var record = new RedisVectorStoreFixture.HotelShortInfo("H1", "My Hotel 1", "This is a great hotel.", null);

        // Act.
        var upsertResult = await sut.UpsertAsync(record);

        // Assert.
        var getResult = await sut.GetAsync("H1");
        Assert.Equal("H1", upsertResult);
        Assert.Equal(record, getResult);

        // Output.
        output.WriteLine(upsertResult);
        output.WriteLine(getResult?.ToString());
    }
}
