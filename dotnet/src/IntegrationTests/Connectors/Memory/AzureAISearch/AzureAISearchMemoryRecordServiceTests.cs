// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureAISearch;
using Microsoft.SemanticKernel.Memory;
using Xunit;
using Xunit.Abstractions;
using static SemanticKernel.IntegrationTests.Connectors.Memory.AzureAISearch.AzureAISearchMemoryFixture;

namespace SemanticKernel.IntegrationTests.Connectors.Memory.AzureAISearch;

/// <summary>
/// Integration tests for <see cref="AzureAISearchMemoryRecordService{TDataModel}"/> class.
/// Tests work with Azure AI Search Instance.
/// </summary>
[Collection("AzureAISearchMemoryCollection")]
public sealed class AzureAISearchMemoryRecordServiceTests(ITestOutputHelper output, AzureAISearchMemoryFixture fixture) : IClassFixture<AzureAISearchMemoryFixture>
{
    // If null, all tests will be enabled
    private const string SkipReason = null; //"Requires Azure AI Search Service instance up and running";

    [Fact(Skip = SkipReason)]
    public async Task ItCanUpsertDocumentToMemoryStoreAsync()
    {
        // Arrange
        var sut = new AzureAISearchMemoryRecordService<Hotel>(fixture.SearchIndexClient, fixture.TestIndexName);

        // Act
        var hotel = new Hotel()
        {
            HotelId = "mh5",
            HotelName = "MyHotel5",
            Description = "My Hotel is great.",
            DescriptionEmbedding = new[] { 30f, 31f, 32f, 33f },
            Tags = new[] { "pool", "air conditioning", "concierge" },
            ParkingIncluded = true,
            LastRenovationDate = new DateTimeOffset(1970, 1, 18, 0, 0, 0, TimeSpan.Zero),
            Rating = 3.6,
            Address = new Address()
            {
                City = "New York",
                Country = "USA"
            }
        };
        var upsertResult = await sut.UpsertAsync(hotel);
        var getResult = await sut.GetAsync("mh5");

        // Assert
        Assert.NotNull(upsertResult);
        Assert.Equal("mh5", upsertResult);

        Assert.NotNull(getResult);
        Assert.Equal(hotel.HotelName, getResult.HotelName);
        Assert.Equal(hotel.Description, getResult.Description);
        Assert.Equal(hotel.DescriptionEmbedding, getResult.DescriptionEmbedding);
        Assert.Equal(hotel.Tags, getResult.Tags);
        Assert.Equal(hotel.ParkingIncluded, getResult.ParkingIncluded);
        Assert.Equal(hotel.LastRenovationDate, getResult.LastRenovationDate);
        Assert.Equal(hotel.Rating, getResult.Rating);
        Assert.Equal(hotel.Address.City, getResult.Address.City);
        Assert.Equal(hotel.Address.Country, getResult.Address.Country);

        // Output
        output.WriteLine(upsertResult);
        output.WriteLine(getResult.ToString());
    }

    [Fact(Skip = SkipReason)]
    public async Task ItCanUpsertManyDocumentsToMemoryStoreAsync()
    {
        // Arrange
        var sut = new AzureAISearchMemoryRecordService<HotelShortInfo>(fixture.SearchIndexClient, fixture.TestIndexName);

        // Act
        var results = sut.UpsertBatchAsync(
            [
                new HotelShortInfo("mh1", "MyHotel1", "My Hotel is great 1."),
                new HotelShortInfo("mh2", "MyHotel2", "My Hotel is great 2."),
                new HotelShortInfo("mh3", "MyHotel3", "My Hotel is great 2."),
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
    public async Task ItCanGetDocumentFromMemoryStoreAsync()
    {
        // Arrange
        var sut = new AzureAISearchMemoryRecordService<HotelShortInfo>(fixture.SearchIndexClient, fixture.TestIndexName);

        // Act
        var hotel1 = await sut.GetAsync("1", new GetRecordOptions { IncludeVectors = true });

        // Assert
        Assert.NotNull(hotel1);

        // Output
        output.WriteLine(hotel1.ToString());
    }

    [Fact(Skip = SkipReason)]
    public async Task ItCanGetManyDocumentsFromMemoryStoreAsync()
    {
        // Arrange
        var sut = new AzureAISearchMemoryRecordService<HotelShortInfo>(fixture.SearchIndexClient, fixture.TestIndexName);

        // Act
        var hotels = sut.GetBatchAsync(["1", "2", "3", "4"], new GetRecordOptions { IncludeVectors = true });

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

    [Fact]
    public async Task ItThrowsForPartialBatchResultAsync()
    {
        // Arrange.
        var sut = new AzureAISearchMemoryRecordService<HotelShortInfo>(fixture.SearchIndexClient, fixture.TestIndexName);

        // Act.
        await Assert.ThrowsAsync<HttpOperationException>(async () => await sut.GetBatchAsync(["1", "5", "2"]).ToListAsync());
    }

    [Fact(Skip = SkipReason)]
    public async Task ItCanRemoveDocumentFromMemoryStoreAsync()
    {
        // Arrange
        var sut = new AzureAISearchMemoryRecordService<HotelShortInfo>(fixture.SearchIndexClient, fixture.TestIndexName);
        await sut.UpsertAsync(new HotelShortInfo("tmp1", "TempHotel1", "This hotel will be deleted."));

        // Act
        await sut.DeleteAsync("tmp1");

        // Assert
        await Assert.ThrowsAsync<HttpOperationException>(async () => await sut.GetAsync("tmp1", new GetRecordOptions { IncludeVectors = true }));
    }

    [Fact(Skip = SkipReason)]
    public async Task ItCanRemoveManyDocumentsFromMemoryStoreAsync()
    {
        // Arrange
        var sut = new AzureAISearchMemoryRecordService<HotelShortInfo>(fixture.SearchIndexClient, fixture.TestIndexName);
        await sut.UpsertAsync(new HotelShortInfo("tmp5", "TempHotel5", "This hotel will be deleted."));
        await sut.UpsertAsync(new HotelShortInfo("tmp6", "TempHotel6", "This hotel will be deleted."));
        await sut.UpsertAsync(new HotelShortInfo("tmp7", "TempHotel7", "This hotel will be deleted."));

        // Act
        await sut.DeleteBatchAsync(["tmp5", "tmp6", "tmp7"]);

        // Assert
        await Assert.ThrowsAsync<HttpOperationException>(async () => await sut.GetAsync("tmp5", new GetRecordOptions { IncludeVectors = true }));
        await Assert.ThrowsAsync<HttpOperationException>(async () => await sut.GetAsync("tmp6", new GetRecordOptions { IncludeVectors = true }));
        await Assert.ThrowsAsync<HttpOperationException>(async () => await sut.GetAsync("tmp7", new GetRecordOptions { IncludeVectors = true }));
    }
}
