// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.SemanticKernel.Memory;

namespace Microsoft.SemanticKernel.Connectors.AzureAISearch;

/// <summary>
/// Class that can create a new collection in an Azure AI Search service using a provided configuration.
/// </summary>
public sealed class AzureAISearchVectorCollectionConfiguredCreate : IVectorCollectionCreate
{
    /// <summary>Azure AI Search client that can be used to manage the list of indices in an Azure AI Search Service.</summary>
    private readonly SearchIndexClient _searchIndexClient;

    /// <summary>Defines the schema of the record type and is used to create the collection with.</summary>
    private readonly VectorStoreRecordDefinition _vectorStoreRecordDefinition;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureAISearchVectorCollectionConfiguredCreate"/> class.
    /// </summary>
    /// <param name="searchIndexClient">Azure AI Search client that can be used to manage the list of indices in an Azure AI Search Service.</param>
    /// <param name="vectorStoreRecordDefinition">Defines the schema of the record type and is used to create the collection with.</param>
    private AzureAISearchVectorCollectionConfiguredCreate(SearchIndexClient searchIndexClient, VectorStoreRecordDefinition vectorStoreRecordDefinition)
    {
        Verify.NotNull(searchIndexClient);
        Verify.NotNull(vectorStoreRecordDefinition);

        this._searchIndexClient = searchIndexClient;
        this._vectorStoreRecordDefinition = vectorStoreRecordDefinition;
    }

    /// <summary>
    /// Create a new instance of <see cref="AzureAISearchVectorCollectionConfiguredCreate"/> using the provided <see cref="VectorStoreRecordDefinition"/>.
    /// </summary>
    /// <param name="searchIndexClient">Azure AI Search client that can be used to manage the list of indices in an Azure AI Search Service.></param>
    /// <param name="vectorStoreRecordDefinition">Defines the schema of the record type and is used to create the collection with.</param>
    /// <returns>The new <see cref="AzureAISearchVectorCollectionConfiguredCreate"/>.</returns>
    public static AzureAISearchVectorCollectionConfiguredCreate Create(SearchIndexClient searchIndexClient, VectorStoreRecordDefinition vectorStoreRecordDefinition)
    {
        return new AzureAISearchVectorCollectionConfiguredCreate(searchIndexClient, vectorStoreRecordDefinition);
    }

    /// <summary>
    /// Create a new instance of <see cref="AzureAISearchVectorCollectionConfiguredCreate"/> by inferring the schema from the provided type and its attributes.
    /// </summary>
    /// <typeparam name="T">The data type to create a collection for.</typeparam>
    /// <param name="searchIndexClient">Azure AI Search client that can be used to manage the list of indices in an Azure AI Search Service.></param>
    /// <returns>The new <see cref="AzureAISearchVectorCollectionConfiguredCreate"/>.</returns>
    public static AzureAISearchVectorCollectionConfiguredCreate Create<T>(SearchIndexClient searchIndexClient)
    {
        var vectorStoreRecordDefinition = VectorStoreRecordPropertyReader.CreateVectorStoreRecordDefinitionFromType(typeof(T), true);
        return new AzureAISearchVectorCollectionConfiguredCreate(searchIndexClient, vectorStoreRecordDefinition);
    }

    /// <inheritdoc />
    public async Task CreateCollectionAsync(string name, CancellationToken cancellationToken = default)
    {
        var vectorSearchConfig = new VectorSearch();
        var searchFields = new List<SearchField>();

        // Loop through all properties and create the search fields.
        foreach (var property in this._vectorStoreRecordDefinition.Properties)
        {
            // Key property.
            if (property is VectorStoreRecordKeyProperty keyProperty)
            {
                searchFields.Add(new SearchableField(keyProperty.PropertyName) { IsKey = true, IsFilterable = true });
            }

            // Data property.
            if (property is VectorStoreRecordDataProperty dataProperty)
            {
                searchFields.Add(new SearchableField(dataProperty.PropertyName) { IsFilterable = dataProperty.IsFilterable });
            }

            // Vector property.
            if (property is VectorStoreRecordVectorProperty vectorProperty)
            {
                if (vectorProperty.Dimensions is not > 0)
                {
                    throw new InvalidOperationException($"Property {nameof(vectorProperty.Dimensions)} on {nameof(VectorStoreRecordVectorProperty)} '{vectorProperty.PropertyName}' must be set to a positive ingeteger to create a collection.");
                }

                // Build a name for the profile and algorithm configuration based on the property name
                // since we'll just create a separate one for each vector property.
                var vectorSearchProfileName = $"{vectorProperty.PropertyName}Profile";
                var algorithmConfigName = $"{vectorProperty.PropertyName}AlgoConfig";

                // Read the vector index settings from the property definition and create the right index configuration.
                var indexKind = GetSKIndexKind(vectorProperty);
                var algorithmMetric = GetSDKAlgorithmMetric(vectorProperty);

                VectorSearchAlgorithmConfiguration algorithmConfiguration = indexKind switch
                {
                    IndexKind.HNSW => new HnswAlgorithmConfiguration(algorithmConfigName) { Parameters = new HnswParameters { Metric = algorithmMetric } },
                    IndexKind.ExhaustiveKNN => new ExhaustiveKnnAlgorithmConfiguration(algorithmConfigName) { Parameters = new ExhaustiveKnnParameters { Metric = algorithmMetric } },
                    _ => throw new InvalidOperationException($"Unsupported index kind '{indexKind}' on {nameof(VectorStoreRecordVectorProperty)} '{vectorProperty.PropertyName}'.")
                };
                var vectorSeachProfile = new VectorSearchProfile(vectorSearchProfileName, algorithmConfigName);

                // Add the search field, plus its profile and algorithm configuration to the search config.
                searchFields.Add(new VectorSearchField(vectorProperty.PropertyName, vectorProperty.Dimensions.Value, vectorSearchProfileName));
                vectorSearchConfig.Algorithms.Add(algorithmConfiguration);
                vectorSearchConfig.Profiles.Add(vectorSeachProfile);
            }
        }

        // Create the index.
        var searchIndex = new SearchIndex(name, searchFields);
        searchIndex.VectorSearch = vectorSearchConfig;
        await this._searchIndexClient.CreateIndexAsync(searchIndex, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Get the configured <see cref="IndexKind"/> from the given <paramref name="vectorProperty"/>.
    /// If none is configured the default is <see cref="IndexKind.HNSW"/>.
    /// </summary>
    /// <param name="vectorProperty">The vector property definition.</param>
    /// <returns>The chosen <see cref="IndexKind"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if a index type was chosen that isn't supported by Azure AI Search.</exception>
    private static IndexKind GetSKIndexKind(VectorStoreRecordVectorProperty vectorProperty)
    {
        if (vectorProperty.IndexKind is null)
        {
            return IndexKind.HNSW;
        }

        return vectorProperty.IndexKind switch
        {
            IndexKind.HNSW => IndexKind.HNSW,
            IndexKind.ExhaustiveKNN => IndexKind.ExhaustiveKNN,
            _ => throw new InvalidOperationException($"Unsupported index kind '{vectorProperty.IndexKind}' for {nameof(VectorStoreRecordVectorProperty)} '{vectorProperty.PropertyName}'.")
        };
    }

    /// <summary>
    /// Get the configured <see cref="VectorSearchAlgorithmMetric"/> from the given <paramref name="vectorProperty"/>.
    /// If none is configured, the default is <see cref="VectorSearchAlgorithmMetric.Cosine"/>.
    /// </summary>
    /// <param name="vectorProperty">The vector property definition.</param>
    /// <returns>The chosen <see cref="VectorSearchAlgorithmMetric"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if a distance function is chosen that isn't suported by Azure AI Search.</exception>
    private static VectorSearchAlgorithmMetric GetSDKAlgorithmMetric(VectorStoreRecordVectorProperty vectorProperty)
    {
        if (vectorProperty.DistanceFunction is null)
        {
            return VectorSearchAlgorithmMetric.Cosine;
        }

        return vectorProperty.DistanceFunction switch
        {
            DistanceFunction.CosineSimilarity => VectorSearchAlgorithmMetric.Cosine,
            DistanceFunction.DotProduct => VectorSearchAlgorithmMetric.DotProduct,
            DistanceFunction.EuclideanDistance => VectorSearchAlgorithmMetric.Euclidean,
            _ => throw new InvalidOperationException($"Unsupported distance function '{vectorProperty.DistanceFunction}' for {nameof(VectorStoreRecordVectorProperty)} '{vectorProperty.PropertyName}'.")
        };
    }
}
