// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel.Memory;
using SemanticKernel.IntegrationTests.TestSettings.Memory;
using Xunit;

namespace SemanticKernel.IntegrationTests.Connectors.Memory.AzureAISearch;

/// <summary>
/// Helper class for setting up and tearing down Azure AI Search indexes for testing purposes.
/// </summary>
public class AzureAISearchMemoryFixture : IAsyncLifetime
{
    /// <summary>
    /// Test index name which consists out of "hotels-" and the machine name with any non-alphanumeric characters removed.
    /// </summary>
#pragma warning disable CA1308 // Normalize strings to uppercase
    private readonly string _testIndexName = "hotels-" + new Regex("[^a-zA-Z0-9]").Replace(Environment.MachineName.ToLowerInvariant(), "");
#pragma warning restore CA1308 // Normalize strings to uppercase

    /// <summary>
    /// Test Configuration setup.
    /// </summary>
    private readonly IConfigurationRoot _configuration = new ConfigurationBuilder()
            .AddJsonFile(path: "testsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile(path: "testsettings.development.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddUserSecrets<AzureAISearchMemoryRecordServiceTests>()
            .Build();

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureAISearchMemoryFixture"/> class.
    /// </summary>
    public AzureAISearchMemoryFixture()
    {
        var config = this._configuration.GetSection("AzureAISearch").Get<AzureAISearchConfiguration>();
        Assert.NotNull(config);
        this.SearchIndexClient = new SearchIndexClient(new Uri(config.ServiceUrl), new AzureKeyCredential(config.ApiKey));
    }

    /// <summary>
    /// Gets the Search Index Client to use for connecting to the Azure AI Search service.
    /// </summary>
    public SearchIndexClient SearchIndexClient { get; private set; }

    /// <summary>
    /// Gets the name of the index that this fixture sets up and tears down.
    /// </summary>
    public string TestIndexName { get => this._testIndexName; }

    /// <summary>
    /// Create / Recreate index and upload documents before test run.
    /// </summary>
    /// <returns>An async task.</returns>
    public async Task InitializeAsync()
    {
        await AzureAISearchMemoryFixture.DeleteIndexIfExistsAsync(this._testIndexName, this.SearchIndexClient);
        await AzureAISearchMemoryFixture.CreateIndexAsync(this._testIndexName, this.SearchIndexClient);
        AzureAISearchMemoryFixture.UploadDocuments(this.SearchIndexClient.GetSearchClient(this._testIndexName));
    }

    /// <summary>
    /// Delete the index after the test run.
    /// </summary>
    /// <returns>An async task.</returns>
    public async Task DisposeAsync()
    {
        await AzureAISearchMemoryFixture.DeleteIndexIfExistsAsync(this._testIndexName, this.SearchIndexClient);
    }

    /// <summary>
    /// Delete the index if it exists.
    /// </summary>
    /// <param name="indexName">The name of the index to delete.</param>
    /// <param name="adminClient">The search index client to use for deleting the index.</param>
    /// <returns>An async task.</returns>
    public static async Task DeleteIndexIfExistsAsync(string indexName, SearchIndexClient adminClient)
    {
        adminClient.GetIndexNames();
        {
            await adminClient.DeleteIndexAsync(indexName);
        }
    }

    /// <summary>
    /// Create an index with the given name.
    /// </summary>
    /// <param name="indexName">The name of the index to create.</param>
    /// <param name="adminClient">The search index client to use for creating the index.</param>
    /// <returns>An async task.</returns>
    public static async Task CreateIndexAsync(string indexName, SearchIndexClient adminClient)
    {
        FieldBuilder fieldBuilder = new();
        var searchFields = fieldBuilder.Build(typeof(Hotel));
        var embeddingfield = searchFields.First(x => x.Name == "DescriptionEmbedding");
        searchFields.Remove(embeddingfield);
        searchFields.Add(new VectorSearchField("DescriptionEmbedding", 4, "my-vector-profile"));

        var definition = new SearchIndex(indexName, searchFields);
        definition.VectorSearch = new VectorSearch();
        definition.VectorSearch.Algorithms.Add(new HnswAlgorithmConfiguration("my-hnsw-vector-config-1") { Parameters = new HnswParameters { Metric = VectorSearchAlgorithmMetric.Cosine } });
        definition.VectorSearch.Profiles.Add(new VectorSearchProfile("my-vector-profile", "my-hnsw-vector-config-1"));

        var suggester = new SearchSuggester("sg", new[] { "HotelName", "Address/City" });
        definition.Suggesters.Add(suggester);

        await adminClient.CreateOrUpdateIndexAsync(definition);
    }

    /// <summary>
    /// Upload test documents to the index.
    /// </summary>
    /// <param name="searchClient">The client to use for uploading the documents.</param>
    public static void UploadDocuments(SearchClient searchClient)
    {
        IndexDocumentsBatch<Hotel> batch = IndexDocumentsBatch.Create(
            IndexDocumentsAction.Upload(
                new Hotel()
                {
                    HotelId = "1",
                    HotelName = "Secret Point Motel",
                    Description = "The hotel is ideally located on the main commercial artery of the city in the heart of New York. A few minutes away is Time's Square and the historic centre of the city, as well as other places of interest that make New York one of America's most attractive and cosmopolitan cities.",
                    DescriptionEmbedding = new[] { 30f, 31f, 32f, 33f },
                    Tags = new[] { "pool", "air conditioning", "concierge" },
                    ParkingIncluded = false,
                    LastRenovationDate = new DateTimeOffset(1970, 1, 18, 0, 0, 0, TimeSpan.Zero),
                    Rating = 3.6,
                    Address = new Address()
                    {
                        City = "New York",
                        Country = "USA"
                    }
                }),
            IndexDocumentsAction.Upload(
                new Hotel()
                {
                    HotelId = "2",
                    HotelName = "Twin Dome Motel",
                    Description = "The hotel is situated in a  nineteenth century plaza, which has been expanded and renovated to the highest architectural standards to create a modern, functional and first-class hotel in which art and unique historical elements coexist with the most modern comforts.",
                    DescriptionEmbedding = new[] { 30f, 31f, 32f, 33f },
                    Tags = new[] { "pool", "free wifi", "concierge" },
                    ParkingIncluded = false,
                    LastRenovationDate = new DateTimeOffset(1979, 2, 18, 0, 0, 0, TimeSpan.Zero),
                    Rating = 3.60,
                    Address = new Address()
                    {
                        City = "Sarasota",
                        Country = "USA"
                    }
                }),
            IndexDocumentsAction.Upload(
                new Hotel()
                {
                    HotelId = "3",
                    HotelName = "Triple Landscape Hotel",
                    Description = "The Hotel stands out for its gastronomic excellence under the management of William Dough, who advises on and oversees all of the Hotel’s restaurant services.",
                    DescriptionEmbedding = new[] { 30f, 31f, 32f, 33f },
                    Tags = new[] { "air conditioning", "bar", "continental breakfast" },
                    ParkingIncluded = true,
                    LastRenovationDate = new DateTimeOffset(2015, 9, 20, 0, 0, 0, TimeSpan.Zero),
                    Rating = 4.80,
                    Address = new Address()
                    {
                        City = "Atlanta",
                        Country = "USA"
                    }
                }),
            IndexDocumentsAction.Upload(
                new Hotel()
                {
                    HotelId = "4",
                    HotelName = "Sublime Cliff Hotel",
                    Description = "Sublime Cliff Hotel is located in the heart of the historic center of Sublime in an extremely vibrant and lively area within short walking distance to the sites and landmarks of the city and is surrounded by the extraordinary beauty of churches, buildings, shops and monuments. Sublime Cliff is part of a lovingly restored 1800 palace.",
                    DescriptionEmbedding = new[] { 30f, 31f, 32f, 33f },
                    Tags = new[] { "concierge", "view", "24-hour front desk service" },
                    ParkingIncluded = true,
                    LastRenovationDate = new DateTimeOffset(1960, 2, 06, 0, 0, 0, TimeSpan.Zero),
                    Rating = 4.60,
                    Address = new Address()
                    {
                        City = "San Antonio",
                        Country = "USA"
                    }
                })
            );

        searchClient.IndexDocuments(batch);
    }

    public record HotelShortInfo(
        [property: MemoryRecordKey] string HotelId,
        [property: MemoryRecordData] string HotelName,
        [property: MemoryRecordData(HasEmbedding = true)] string Description);

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public record Hotel
    {
        [SimpleField(IsKey = true, IsFilterable = true)]
        [MemoryRecordKey]
        public string HotelId { get; set; }

        [SearchableField(IsSortable = true)]
        [MemoryRecordData]
        public string HotelName { get; set; }

        [SearchableField(AnalyzerName = LexicalAnalyzerName.Values.EnLucene)]
        [MemoryRecordData(HasEmbedding = true, EmbeddingPropertyName = "DescriptionEmbedding")]
        public string Description { get; set; }

        [MemoryRecordVector]
        public ReadOnlyMemory<float> DescriptionEmbedding { get; set; }

        [SearchableField(IsFilterable = true, IsFacetable = true)]
        [MemoryRecordData]
#pragma warning disable CA1819 // Properties should not return arrays
        public string[] Tags { get; set; }
#pragma warning restore CA1819 // Properties should not return arrays

        [SimpleField(IsFilterable = true, IsSortable = true, IsFacetable = true)]
        [MemoryRecordData]
        public bool? ParkingIncluded { get; set; }

        [SimpleField(IsFilterable = true, IsSortable = true, IsFacetable = true)]
        [MemoryRecordData]
        public DateTimeOffset? LastRenovationDate { get; set; }

        [SimpleField(IsFilterable = true, IsSortable = true, IsFacetable = true)]
        [MemoryRecordData]
        public double? Rating { get; set; }

        [SearchableField]
        [MemoryRecordData]
        public Address Address { get; set; }
    }

    public record Address
    {
        [SearchableField(IsFilterable = true, IsSortable = true, IsFacetable = true)]
        public string City { get; set; }

        [SearchableField(IsFilterable = true, IsSortable = true, IsFacetable = true)]
        public string Country { get; set; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
}
