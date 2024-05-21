// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using Microsoft.SemanticKernel.Memory;

namespace Microsoft.SemanticKernel.Connectors.AzureAISearch;

/// <summary>
/// Vector store that uses Azure AI Search as the underlying storage.
/// </summary>
/// <typeparam name="TDataModel">The data model to use for adding, updating and retrieving data from storage.</typeparam>
public class AzureAISearchVectorDBRecordService<TDataModel> : IVectorDBRecordService<string, TDataModel>
    where TDataModel : class
{
    /// <summary>Azure AI Search client that can be used to manage the list of indices in an Azure AI Search Service.</summary>
    private readonly SearchIndexClient _searchIndexClient;

    /// <summary>The name of the collection to use with this store if none is provided for any individual operation.</summary>
    private readonly string _defaultCollectionName;

    /// <summary>The name of the key field for the collections that this class is used with.</summary>
    private readonly string _keyFieldName;

    /// <summary>Azure AI Search clients that can be used to manage data in an Azure AI Search Service index.</summary>
    private readonly ConcurrentDictionary<string, SearchClient> _searchClientsByIndex = new();

    /// <summary>Optional configuration options for this class.</summary>
    private readonly AzureAISearchVectorDBRecordServiceOptions _options;

    /// <summary>The names of all non vector fields on the current model.</summary>
    private readonly List<string> _nonVectorFieldNames;

    /// <summary>
    /// Create a new instance of vector storage using Azure AI Search.
    /// </summary>
    /// <param name="searchIndexClient">Azure AI Search client that can be used to manage the list of indices in an Azure AI Search Service.</param>
    /// <param name="defaultCollectionName">The name of the collection to use with this store if none is provided for any individual operation.</param>
    /// <param name="keyFieldName">The name of the key field for the collection that this class is used with.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="searchIndexClient"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="defaultCollectionName"/> or <paramref name="keyFieldName"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the <see cref="AzureAISearchVectorDBRecordServiceOptions.MaxDegreeOfGetParallelism"/> setting is less than 1.</exception>
    public AzureAISearchVectorDBRecordService(SearchIndexClient searchIndexClient, string defaultCollectionName, string keyFieldName, AzureAISearchVectorDBRecordServiceOptions? options = default)
    {
        Verify.NotNull(searchIndexClient);
        Verify.NotNullOrWhiteSpace(defaultCollectionName);
        Verify.NotNullOrWhiteSpace(keyFieldName);

        this._searchIndexClient = searchIndexClient;
        this._defaultCollectionName = defaultCollectionName;
        this._keyFieldName = keyFieldName;
        this._options = options ?? new AzureAISearchVectorDBRecordServiceOptions();

        if (this._options.MaxDegreeOfGetParallelism is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "AzureAISearchVectorStoreOptions.MaxDegreeOfGetParallelism must be greater than 0.");
        }

        // Build the list of field names on the curent model that don't have the VectorStoreModelVectorAtrribute but has
        // the VectorStoreModelKeyAttribute or VectorStoreModelDataAtrribute or VectorStoreModelMetadataAtrribute attributes.
        this._nonVectorFieldNames = typeof(TDataModel).GetProperties()
            .Where(x => x.GetCustomAttributes(true).Select(x => x.GetType()).Intersect([typeof(KeyAttribute), typeof(DataAttribute), typeof(MetadataAttribute)]).Any())
            .Select(x => x.Name)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<TDataModel?> GetAsync(string key, Memory.GetRecordOptions? options = default, CancellationToken cancellationToken = default)
    {
        Verify.NotNullOrWhiteSpace(key);

        // Create Options.
        var innerOptions = this.ConvertGetDocumentOptions(options);
        var collectionName = options?.CollectionName ?? this._defaultCollectionName;

        // Get record.
        var searchClient = this.GetSearchClient(collectionName);
        return await RunOperationAsync(() => searchClient.GetDocumentAsync<TDataModel>(key, innerOptions, cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TDataModel> GetBatchAsync(IEnumerable<string> keys, Memory.GetRecordOptions? options = default, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Verify.NotNull(keys);

        // Create Options
        var innerOptions = this.ConvertGetDocumentOptions(options);
        var collectionName = options?.CollectionName ?? this._defaultCollectionName;

        // Split keys into batches
        var maxDegreeOfGetParallelism = this._options?.MaxDegreeOfGetParallelism ?? 50;
        var batches = keys.SplitIntoBatches(maxDegreeOfGetParallelism);

        var searchClient = this.GetSearchClient(collectionName);
        foreach (var batch in batches)
        {
            // Get each batch
            var tasks = batch.Select(key => RunOperationAsync(() => searchClient.GetDocumentAsync<TDataModel>(key, innerOptions, cancellationToken)));
            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            foreach (var result in results) { yield return result; }
        }
    }

    /// <inheritdoc />
    public async Task<string> RemoveAsync(string key, RemoveRecordOptions? options = default, CancellationToken cancellationToken = default)
    {
        Verify.NotNullOrWhiteSpace(key);

        // Create options.
        var collectionName = options?.CollectionName ?? this._defaultCollectionName;

        // Remove record.
        var searchClient = this.GetSearchClient(collectionName);
        var results = await RunOperationAsync(() => searchClient.DeleteDocumentsAsync(this._keyFieldName, [key], new IndexDocumentsOptions(), cancellationToken)).ConfigureAwait(false);
        return results.Value.Results[0].Key;
    }

    /// <inheritdoc />
    public async Task RemoveBatchAsync(IEnumerable<string> keys, RemoveRecordOptions? options = default, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(keys);

        // Create options.
        var collectionName = options?.CollectionName ?? this._defaultCollectionName;

        // Remove records.
        var searchClient = this.GetSearchClient(collectionName);
        var results = await RunOperationAsync(() => searchClient.DeleteDocumentsAsync(this._keyFieldName, keys, new IndexDocumentsOptions(), cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string> UpsertAsync(TDataModel record, UpsertRecordOptions? options = default, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(record);

        // Create options.
        var collectionName = options?.CollectionName ?? this._defaultCollectionName;

        // Upsert record.
        var searchClient = this.GetSearchClient(collectionName);
        var results = await RunOperationAsync(() => searchClient.UploadDocumentsAsync<TDataModel>([record], new IndexDocumentsOptions(), cancellationToken)).ConfigureAwait(false);
        return results.Value.Results[0].Key;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> UpsertBatchAsync(IEnumerable<TDataModel> records, UpsertRecordOptions? options = default, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Verify.NotNull(records);

        // Create Options
        var collectionName = options?.CollectionName ?? this._defaultCollectionName;
        var innerOptions = new IndexDocumentsOptions { ThrowOnAnyError = true };

        // Upload data
        var searchClient = this.GetSearchClient(collectionName);
        var results = await RunOperationAsync(
            () => searchClient.IndexDocumentsAsync(
                IndexDocumentsBatch.Upload(records),
                innerOptions,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        // Get results
        var resultKeys = results.Value.Results.Select(x => x.Key).ToList();
        foreach (var resultKey in resultKeys) { yield return resultKey; }
    }

    /// <summary>
    /// Get a search client for the index specified.
    /// Note: the index might not exist, but we avoid checking everytime and the extra latency.
    /// </summary>
    /// <param name="indexName">Index name</param>
    /// <returns>Search client ready to read/write</returns>
    private SearchClient GetSearchClient(string indexName)
    {
        // Check the local cache first, if not found create a new one.
        if (!this._searchClientsByIndex.TryGetValue(indexName, out SearchClient? client))
        {
            client = this._searchIndexClient.GetSearchClient(indexName);
            this._searchClientsByIndex[indexName] = client;
        }

        return client;
    }

    /// <summary>
    /// Convert the public <see cref="Memory.GetRecordOptions"/> options model to the azure ai search <see cref="Azure.Search.Documents.GetDocumentOptions"/> options model.
    /// </summary>
    /// <param name="options">The public options model.</param>
    /// <returns>The azure ai search options model.</returns>
    private Azure.Search.Documents.GetDocumentOptions ConvertGetDocumentOptions(Memory.GetRecordOptions? options)
    {
        var innerOptions = new Azure.Search.Documents.GetDocumentOptions();
        if (options?.IncludeVectors is false)
        {
            innerOptions.SelectedFields.AddRange(this._nonVectorFieldNames);
        }

        return innerOptions;
    }

    /// <summary>
    /// Run the given operation and convert any <see cref="RequestFailedException"/> to <see cref="HttpOperationException"/>."/>
    /// </summary>
    /// <typeparam name="T">The response type of the operation.</typeparam>
    /// <param name="operation">The operation to run.</param>
    /// <returns>The result of the operation.</returns>
    private static async Task<T> RunOperationAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation.Invoke().ConfigureAwait(false);
        }
        catch (RequestFailedException e)
        {
            throw e.ToHttpOperationException();
        }
    }
}
