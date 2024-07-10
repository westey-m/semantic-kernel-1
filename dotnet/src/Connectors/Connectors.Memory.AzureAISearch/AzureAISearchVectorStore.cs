// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.SemanticKernel.Data;

namespace Microsoft.SemanticKernel.Connectors.AzureAISearch;

/// <summary>
/// Vector storage for Azure AI Search.
/// </summary>
public sealed class AzureAISearchVectorStore : IVectorStore
{
    /// <summary>Azure AI Search client that can be used to manage the list of indices in an Azure AI Search Service.</summary>
    private readonly SearchIndexClient _searchIndexClient;

    /// <summary>Optional configuration options for this class.</summary>
    private readonly AzureAISearchVectorStoreOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureAISearchVectorStore"/> class.
    /// </summary>
    /// <param name="searchIndexClient">Azure AI Search client that can be used to manage the list of indices in an Azure AI Search Service.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    public AzureAISearchVectorStore(SearchIndexClient searchIndexClient, AzureAISearchVectorStoreOptions? options = default)
    {
        Verify.NotNull(searchIndexClient);

        this._searchIndexClient = searchIndexClient;
        this._options = options ?? new AzureAISearchVectorStoreOptions();
    }

    /// <inheritdoc />
    public IVectorRecordStore<TKey, TRecord> GetCollection<TKey, TRecord>(string name, VectorStoreRecordDefinition? vectorStoreRecordDefinition = null) where TRecord : class
    {
        if (typeof(TKey) != typeof(string))
        {
            throw new NotSupportedException("Only string keys are supported.");
        }

        if (this._options.VectorStoreCollectionFactory is not null)
        {
            return this._options.VectorStoreCollectionFactory.CreateVectorStoreCollection<TKey, TRecord>(this._searchIndexClient, name, vectorStoreRecordDefinition);
        }

        var directlyCreatedStore = new AzureAISearchVectorRecordStore<TRecord>(this._searchIndexClient, name, new AzureAISearchVectorRecordStoreOptions<TRecord>() { VectorStoreRecordDefinition = vectorStoreRecordDefinition }) as IVectorRecordStore<TKey, TRecord>;
        return directlyCreatedStore!;
    }

    /// <inheritdoc />
    public async Task<IVectorRecordStore<TKey, TRecord>> CreateCollectionAsync<TKey, TRecord>(string name, VectorStoreRecordDefinition? vectorStoreRecordDefinition = null, CancellationToken cancellationToken = default) where TRecord : class
    {
        if (typeof(TKey) != typeof(string))
        {
            throw new NotSupportedException("Only string keys are supported.");
        }

        var vectorSearchConfig = new VectorSearch();
        var searchFields = new List<SearchField>();

        if (vectorStoreRecordDefinition is null)
        {
            vectorStoreRecordDefinition = VectorStoreRecordPropertyReader.CreateVectorStoreRecordDefinitionFromType(typeof(TRecord), true);
        }

        // Loop through all properties and create the search fields.
        foreach (var property in vectorStoreRecordDefinition.Properties)
        {
            // Key property.
            if (property is VectorStoreRecordKeyProperty keyProperty)
            {
                searchFields.Add(new SearchableField(keyProperty.PropertyName) { IsKey = true, IsFilterable = true });
            }

            // Data property.
            if (property is VectorStoreRecordDataProperty dataProperty)
            {
                if (dataProperty.PropertyType == typeof(string))
                {
                    searchFields.Add(new SearchableField(dataProperty.PropertyName) { IsFilterable = dataProperty.IsFilterable });
                }
                else
                {
                    if (dataProperty.PropertyType is null)
                    {
                        throw new InvalidOperationException($"Property {nameof(dataProperty.PropertyType)} on {nameof(VectorStoreRecordDataProperty)} '{dataProperty.PropertyName}' must be set to create a collection.");
                    }

                    searchFields.Add(new SimpleField(dataProperty.PropertyName, AzureAISearchVectorStoreCollectionCreateMapping.GetSDKFieldDataType(dataProperty.PropertyType)) { IsFilterable = dataProperty.IsFilterable });
                }
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
                var indexKind = AzureAISearchVectorStoreCollectionCreateMapping.GetSKIndexKind(vectorProperty);
                var algorithmMetric = AzureAISearchVectorStoreCollectionCreateMapping.GetSDKDistanceAlgorithm(vectorProperty);

                VectorSearchAlgorithmConfiguration algorithmConfiguration = indexKind switch
                {
                    IndexKind.Hnsw => new HnswAlgorithmConfiguration(algorithmConfigName) { Parameters = new HnswParameters { Metric = algorithmMetric } },
                    IndexKind.Flat => new ExhaustiveKnnAlgorithmConfiguration(algorithmConfigName) { Parameters = new ExhaustiveKnnParameters { Metric = algorithmMetric } },
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

        return this.GetCollection<TKey, TRecord>(name, vectorStoreRecordDefinition);
    }

    /// <inheritdoc />
    public async Task<IVectorRecordStore<TKey, TRecord>> CreateCollectionIfNotExistsAsync<TKey, TRecord>(string name, VectorStoreRecordDefinition? vectorStoreRecordDefinition = null, CancellationToken cancellationToken = default) where TRecord : class
    {
        if (!await this.CollectionExistsAsync(name, cancellationToken).ConfigureAwait(false))
        {
            return await this.CreateCollectionAsync<TKey, TRecord>(name, vectorStoreRecordDefinition, cancellationToken).ConfigureAwait(false);
        }

        return this.GetCollection<TKey, TRecord>(name, vectorStoreRecordDefinition);
    }

    /// <inheritdoc />
    public async Task DeleteCollectionAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            await this._searchIndexClient.DeleteIndexAsync(name, cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException e)
        {
            throw e.ToHttpOperationException();
        }
    }

    /// <inheritdoc />
    public async Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        Verify.NotNullOrWhiteSpace(name);

        try
        {
            var getResult = await this._searchIndexClient.GetIndexAsync(name, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
        catch (RequestFailedException e)
        {
            throw e.ToHttpOperationException();
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> ListCollectionNamesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var indexNamesEnumerable = this._searchIndexClient.GetIndexNamesAsync(cancellationToken).ConfigureAwait(false);
        var indexNamesEnumerator = indexNamesEnumerable.GetAsyncEnumerator();

        var nextResult = await GetNextIndexNameAsync(indexNamesEnumerator).ConfigureAwait(false);
        while (nextResult.more)
        {
            yield return nextResult.name;
            nextResult = await GetNextIndexNameAsync(indexNamesEnumerator).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Helper method to get the next index name from the enumerator with a try catch around the move next call to convert
    /// any <see cref="RequestFailedException"/> to <see cref="HttpOperationException"/>, since try catch is not supported
    /// around a yield return.
    /// </summary>
    /// <param name="enumerator">The enumerator to get the next result from.</param>
    /// <returns>A value indicating whether there are more results and the current string if true.</returns>
    private static async Task<(string name, bool more)> GetNextIndexNameAsync(ConfiguredCancelableAsyncEnumerable<string>.Enumerator enumerator)
    {
        try
        {
            var more = await enumerator.MoveNextAsync();
            return (enumerator.Current, more);
        }
        catch (RequestFailedException e)
        {
            throw e.ToHttpOperationException();
        }
    }
}
