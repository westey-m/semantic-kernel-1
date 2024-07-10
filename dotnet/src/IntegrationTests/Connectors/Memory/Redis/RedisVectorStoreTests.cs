// Copyright (c) Microsoft. All rights reserved.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Connectors.Redis;
using NRedisStack.RedisStackCommands;
using NRedisStack.Search;
using Xunit;
using Xunit.Abstractions;

namespace SemanticKernel.IntegrationTests.Connectors.Memory.Redis;

[Collection("RedisVectorStoreCollection")]
public class RedisVectorStoreTests(ITestOutputHelper output, RedisVectorStoreFixture fixture)
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ItCanCreateACollectionAsync(bool useDefinition)
    {
        // Arrange
        var collectionNamePostfix = useDefinition ? "withDefinition" : "withType";
        var testCollectionName = $"createtest{collectionNamePostfix}";
        var sut = new RedisVectorStore(fixture.Database);

        // Act
        await sut.CreateCollectionAsync<string, RedisVectorStoreFixture.Hotel>(testCollectionName, useDefinition ? fixture.VectorStoreRecordDefinition : null);

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
        var sut = new RedisVectorStore(fixture.Database);

        // Act.
        var doesExistResult = await sut.CollectionExistsAsync("hotels");

        // Assert.
        Assert.True(doesExistResult);

        // Output.
        output.WriteLine(doesExistResult.ToString());
    }

    [Fact]
    public async Task ItCanCheckIfCollectionExistsForNonExistingCollectionAsync()
    {
        // Arrange.
        var sut = new RedisVectorStore(fixture.Database);

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
        var sut = new RedisVectorStore(fixture.Database);

        // Act
        var collectionNames = await sut.ListCollectionNamesAsync().ToListAsync();

        // Assert
        Assert.Single(collectionNames);
        Assert.Contains("hotels", collectionNames);

        // Output
        output.WriteLine(string.Join(",", collectionNames));
    }

    [Fact]
    public async Task ItCanDeleteACollectionAsync()
    {
        // Arrange
        var tempCollectionName = "temp-test";
        var schema = new Schema();
        schema.AddTextField("HotelName");
        var createParams = new FTCreateParams();
        createParams.AddPrefix(tempCollectionName);
        await fixture.Database.FT().CreateAsync(tempCollectionName, createParams, schema);

        var sut = new RedisVectorStore(fixture.Database);

        // Act
        await sut.DeleteCollectionAsync(tempCollectionName);

        // Assert
        Assert.False(await sut.CollectionExistsAsync(tempCollectionName));
    }
}
