// Copyright (c) Microsoft. All rights reserved.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Connectors.Redis;
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
        var collection = sut.GetCollection<string, RedisVectorStoreFixture.Hotel>(testCollectionName);

        // Act
        await sut.CreateCollectionAsync<string, RedisVectorStoreFixture.Hotel>(testCollectionName, useDefinition ? fixture.VectorStoreRecordDefinition : null);

        // Assert
        var existResult = await collection.CollectionExistsAsync();
        Assert.True(existResult);
        await collection.DeleteCollectionAsync();

        // Output
        output.WriteLine(existResult.ToString());
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
}
