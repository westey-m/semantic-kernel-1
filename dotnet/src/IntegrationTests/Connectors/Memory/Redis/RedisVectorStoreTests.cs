// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Redis;
using Xunit;
using Xunit.Abstractions;

namespace SemanticKernel.IntegrationTests.Connectors.Memory.Redis;

[Collection("RedisVectorStoreCollection")]
public sealed class RedisVectorStoreTests(ITestOutputHelper output, RedisVectorStoreFixture fixture)
{
    [Fact]
    public async Task ItCanGetDocumentFromVectorStoreAsync()
    {
        // Arrange.
        var sut = new RedisVectorStore<RedisVectorStoreFixture.HotelShortInfo>(fixture.Database);

        // Act.
        var getResult = await sut.GetAsync("hotels", "H10");

        // Assert.
        Assert.Equal("H10", getResult?.HotelId);

        // Output.
        output.WriteLine(getResult?.ToString());
    }

    [Fact]
    public async Task ItCanRemoveDocumentFromVectorStoreAsync()
    {
        // Arrange.
        var sut = new RedisVectorStore<RedisVectorStoreFixture.HotelShortInfo>(fixture.Database);
        var record = new RedisVectorStoreFixture.HotelShortInfo("TMP20", "My Hotel 20", "This is a great hotel.", Array.Empty<float>());
        await sut.UpsertAsync("hotels", record);

        // Act.
        var removeResult = await sut.RemoveAsync("hotels", "TMP20");

        // Assert.
        Assert.Equal("TMP20", removeResult);
        await Assert.ThrowsAsync<HttpOperationException>(async () => await sut.GetAsync("hotels", "TMP20"));

        // Output.
        output.WriteLine(removeResult);
    }

    [Fact]
    public async Task ItCanUpsertDocumentToVectorStoreAsync()
    {
        // Arrange.
        var sut = new RedisVectorStore<RedisVectorStoreFixture.HotelShortInfo>(fixture.Database);
        var record = new RedisVectorStoreFixture.HotelShortInfo("H1", "My Hotel 1", "This is a great hotel.", Array.Empty<float>());

        // Act.
        var upsertResult = await sut.UpsertAsync("hotels", record);

        // Assert.
        var getResult = await sut.GetAsync("hotels", "H1");
        Assert.Equal("H1", upsertResult);
        Assert.Equal(record, getResult);

        // Output.
        output.WriteLine(upsertResult);
        output.WriteLine(getResult?.ToString());
    }
}
