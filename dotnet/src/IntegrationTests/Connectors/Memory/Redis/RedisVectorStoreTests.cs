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
