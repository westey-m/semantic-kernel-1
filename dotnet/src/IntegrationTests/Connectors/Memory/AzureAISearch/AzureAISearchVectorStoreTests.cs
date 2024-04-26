// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureAISearch;
using Microsoft.SemanticKernel.Memory;
using SemanticKernel.IntegrationTests.Connectors.Memory.AzureAISearch;
using Xunit;
using Xunit.Abstractions;

namespace SemanticKernel.IntegrationTests.Connectors.AzureAISearch;

/// <summary>
/// Integration tests for <see cref="AzureAISearchVectorStore{TDataModel}"/> class.
/// Tests work with Azure AI Search Instance..
/// </summary>
public sealed class AzureAISearchVectorStoreTests(ITestOutputHelper output, AzureAISearchVectorStoreFixture fixture) : IClassFixture<AzureAISearchVectorStoreFixture>
{
    // If null, all tests will be enabled
    private const string SkipReason = null; //"Requires Azure AI Search Service instance up and running";

    private const string KeyFieldName = "HotelId";

    [Fact(Skip = SkipReason)]
    public async Task ItCanUpsertDocumentToVectorStoreAsync()
    {
        // Arrange
        var searchClient = fixture.SearchIndexClient.GetSearchClient(fixture.TestIndexName);
        var sut = new AzureAISearchVectorStore<AzureAISearchVectorStoreFixture.HotelShortInfo>(searchClient, KeyFieldName);

        // Act
        var upsertResult = await sut.UpsertAsync(new AzureAISearchVectorStoreFixture.HotelShortInfo("mh5", "MyHotel5", "My Hotel is great."));
        var getResult = await sut.GetAsync("mh5");

        // Assert
        Assert.NotNull(upsertResult);
        Assert.Equal("mh5", upsertResult);

        Assert.NotNull(getResult);
        Assert.Equal("MyHotel5", getResult.HotelName);

        // Output
        output.WriteLine(upsertResult);
        output.WriteLine(getResult.ToString());
    }

    [Fact(Skip = SkipReason)]
    public async Task ItCanUpsertManyDocumentsToVectorStoreAsync()
    {
        // Arrange
        var searchClient = fixture.SearchIndexClient.GetSearchClient(fixture.TestIndexName);
        var sut = new AzureAISearchVectorStore<AzureAISearchVectorStoreFixture.HotelShortInfo>(searchClient, KeyFieldName);

        // Act
        var results = sut.UpsertBatchAsync(
            [
                new AzureAISearchVectorStoreFixture.HotelShortInfo("mh1", "MyHotel1", "My Hotel is great 1."),
                new AzureAISearchVectorStoreFixture.HotelShortInfo("mh2", "MyHotel2", "My Hotel is great 2."),
                new AzureAISearchVectorStoreFixture.HotelShortInfo("mh3", "MyHotel3", "My Hotel is great 2."),
            ]);

        // Assert
        Assert.NotNull(results);
        var resultsList = await results.ToListAsync();

        Assert.Equal(3, resultsList.Count);
        Assert.Contains("mh1", resultsList);
        Assert.Contains("mh2", resultsList);
        Assert.Contains("mh3", resultsList);

        // Output
        foreach (var result in resultsList)
        {
            output.WriteLine(result);
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task ItCanGetDocumentFromVectorStoreAsync()
    {
        // Arrange
        var searchClient = fixture.SearchIndexClient.GetSearchClient(fixture.TestIndexName);
        var sut = new AzureAISearchVectorStore<AzureAISearchVectorStoreFixture.HotelShortInfo>(searchClient, KeyFieldName);

        // Act
        var hotel1 = await sut.GetAsync("1", new VectorStoreGetDocumentOptions { SelectedFields = new List<string>() { "HotelId", "HotelName" } });

        // Assert
        Assert.NotNull(hotel1);

        // Output
        output.WriteLine(hotel1.ToString());
    }

    [Fact(Skip = SkipReason)]
    public async Task ItCanGetManyDocumentsFromVectorStoreAsync()
    {
        // Arrange
        var searchClient = fixture.SearchIndexClient.GetSearchClient(fixture.TestIndexName);
        var options = new AzureAISearchVectorStoreOptions { MaxDegreeOfGetParallelism = 3 };
        var sut = new AzureAISearchVectorStore<AzureAISearchVectorStoreFixture.HotelShortInfo>(searchClient, KeyFieldName, options);

        // Act
        var hotels = sut.GetBatchAsync(["1", "2", "3", "4"], new VectorStoreGetDocumentOptions { SelectedFields = new List<string>() { "HotelId", "HotelName", "Description" } });

        // Assert
        Assert.NotNull(hotels);
        var hotelsList = await hotels.ToListAsync();
        Assert.Equal(4, hotelsList.Count);

        // Output
        foreach (var hotel in hotelsList)
        {
            output.WriteLine(hotel.ToString());
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task ItCanRemoveDocumentFromVectorStoreAsync()
    {
        // Arrange
        var searchClient = fixture.SearchIndexClient.GetSearchClient(fixture.TestIndexName);
        var sut = new AzureAISearchVectorStore<AzureAISearchVectorStoreFixture.HotelShortInfo>(searchClient, KeyFieldName);
        await sut.UpsertAsync(new AzureAISearchVectorStoreFixture.HotelShortInfo("tmp1", "TempHotel1", "This hotel will be deleted."));

        // Act
        await sut.RemoveAsync("tmp1");

        // Assert
        await Assert.ThrowsAsync<HttpOperationException>(async () => await sut.GetAsync("tmp1", new VectorStoreGetDocumentOptions { SelectedFields = new List<string>() { "HotelId", "HotelName" } }));
    }

    [Fact(Skip = SkipReason)]
    public async Task ItCanRemoveManyDocumentsFromVectorStoreAsync()
    {
        // Arrange
        var searchClient = fixture.SearchIndexClient.GetSearchClient(fixture.TestIndexName);
        var sut = new AzureAISearchVectorStore<AzureAISearchVectorStoreFixture.HotelShortInfo>(searchClient, KeyFieldName);
        await sut.UpsertAsync(new AzureAISearchVectorStoreFixture.HotelShortInfo("tmp5", "TempHotel5", "This hotel will be deleted."));
        await sut.UpsertAsync(new AzureAISearchVectorStoreFixture.HotelShortInfo("tmp6", "TempHotel6", "This hotel will be deleted."));
        await sut.UpsertAsync(new AzureAISearchVectorStoreFixture.HotelShortInfo("tmp7", "TempHotel7", "This hotel will be deleted."));

        // Act
        await sut.RemoveBatchAsync(["tmp5", "tmp6", "tmp7"]);

        // Assert
        await Assert.ThrowsAsync<HttpOperationException>(async () => await sut.GetAsync("tmp5", new VectorStoreGetDocumentOptions { SelectedFields = new List<string>() { "HotelId", "HotelName" } }));
        await Assert.ThrowsAsync<HttpOperationException>(async () => await sut.GetAsync("tmp6", new VectorStoreGetDocumentOptions { SelectedFields = new List<string>() { "HotelId", "HotelName" } }));
        await Assert.ThrowsAsync<HttpOperationException>(async () => await sut.GetAsync("tmp7", new VectorStoreGetDocumentOptions { SelectedFields = new List<string>() { "HotelId", "HotelName" } }));
    }
}
