// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
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
    /// <summary>A set of types that a key on the provided model may have.</summary>
    private static readonly HashSet<Type> s_supportedKeyTypes = new()
    {
        typeof(string)
    };

    /// <summary>A set of types that vectors on the provided model may have.</summary>
    private static readonly HashSet<Type> s_supportedVectorTypes = new()
    {
        typeof(ReadOnlyMemory<float>),
        typeof(ReadOnlyMemory<float>?)
    };

    /// <summary>Azure AI Search client that can be used to manage the list of indices in an Azure AI Search Service.</summary>
    private readonly SearchIndexClient _searchIndexClient;

    /// <summary>The name of the collection to use with this store if none is provided for any individual operation.</summary>
    private readonly string _defaultCollectionName;

    /// <summary>The name of the key field for the collections that this class is used with.</summary>
    private readonly string _keyFieldName;

    /// <summary>Azure AI Search clients that can be used to manage data in an Azure AI Search Service index.</summary>
    private readonly ConcurrentDictionary<string, SearchClient> _searchClientsByIndex = new();

    /// <summary>Optional configuration options for this class.</summary>
    private readonly AzureAISearchVectorDBRecordServiceOptions<TDataModel> _options;

    /// <summary>The names of all non vector fields on the current model.</summary>
    private readonly List<string> _nonVectorFieldNames;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureAISearchVectorDBRecordService{TDataModel}"/> class.
    /// </summary>
    /// <param name="searchIndexClient">Azure AI Search client that can be used to manage the list of indices in an Azure AI Search Service.</param>
    /// <param name="defaultCollectionName">The name of the collection to use with this store if none is provided for any individual operation.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="searchIndexClient"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="defaultCollectionName"/> is null or whitespace.</exception>
    public AzureAISearchVectorDBRecordService(SearchIndexClient searchIndexClient, string defaultCollectionName, AzureAISearchVectorDBRecordServiceOptions<TDataModel>? options = default)
    {
        // Verify.
        Verify.NotNull(searchIndexClient);
        Verify.NotNullOrWhiteSpace(defaultCollectionName);

        // Assign.
        this._searchIndexClient = searchIndexClient;
        this._defaultCollectionName = defaultCollectionName;
        this._options = options ?? new AzureAISearchVectorDBRecordServiceOptions<TDataModel>();

        // Verify custom mapper.
        if (this._options.MapperType == AzureAISearchVectorDBRecordMapperType.JsonObjectCustomerMapper && this._options.JsonObjectCustomMapper is null)
        {
            throw new ArgumentException($"The {nameof(AzureAISearchVectorDBRecordServiceOptions<TDataModel>.JsonObjectCustomMapper)} option needs to be set if a {nameof(AzureAISearchVectorDBRecordServiceOptions<TDataModel>.MapperType)} of {nameof(AzureAISearchVectorDBRecordMapperType.JsonObjectCustomerMapper)} has been chosen.", nameof(options));
        }

        // Enumerate public properties/fields on model, validate, and store for later use.
        var fields = VectorStoreModelPropertyReader.FindFields(typeof(TDataModel), true);
        VectorStoreModelPropertyReader.VerifyFieldTypes([fields.keyField], s_supportedKeyTypes, "Key");
        VectorStoreModelPropertyReader.VerifyFieldTypes(fields.vectorFields, s_supportedVectorTypes, "Vector");
        this._keyFieldName = fields.keyField.Name;

        // Build the list of field names from the current model that are either key or data fields.
        this._nonVectorFieldNames = fields.dataFields.Concat([fields.keyField]).Select(x => x.Name).ToList();
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
        return await RunOperationAsync(() => this.GetDocumentAndMapToDataModelAsync(searchClient, key, innerOptions, cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TDataModel> GetBatchAsync(IEnumerable<string> keys, Memory.GetRecordOptions? options = default, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Verify.NotNull(keys);

        // Create Options
        var innerOptions = this.ConvertGetDocumentOptions(options);
        var collectionName = options?.CollectionName ?? this._defaultCollectionName;

        // Get records in parallel.
        var searchClient = this.GetSearchClient(collectionName);
        var tasks = keys.Select(key => RunOperationAsync(() => this.GetDocumentAndMapToDataModelAsync(searchClient, key, innerOptions, cancellationToken)));
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        foreach (var result in results) { yield return result; }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string key, DeleteRecordOptions? options = default, CancellationToken cancellationToken = default)
    {
        Verify.NotNullOrWhiteSpace(key);

        // Create options.
        var collectionName = options?.CollectionName ?? this._defaultCollectionName;

        // Remove record.
        var searchClient = this.GetSearchClient(collectionName);
        var results = await RunOperationAsync(() => searchClient.DeleteDocumentsAsync(this._keyFieldName, [key], new IndexDocumentsOptions(), cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteBatchAsync(IEnumerable<string> keys, DeleteRecordOptions? options = default, CancellationToken cancellationToken = default)
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
        var innerOptions = new IndexDocumentsOptions { ThrowOnAnyError = true };

        // Upsert record.
        var searchClient = this.GetSearchClient(collectionName);
        var results = await RunOperationAsync(() => this.MapToStorageModelAndUploadDocumentAsync(searchClient, [record], innerOptions, cancellationToken)).ConfigureAwait(false);
        return results.Value.Results[0].Key;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> UpsertBatchAsync(IEnumerable<TDataModel> records, UpsertRecordOptions? options = default, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Verify.NotNull(records);

        // Create Options
        var collectionName = options?.CollectionName ?? this._defaultCollectionName;
        var innerOptions = new IndexDocumentsOptions { ThrowOnAnyError = true };

        // Upsert records
        var searchClient = this.GetSearchClient(collectionName);
        var results = await RunOperationAsync(() => this.MapToStorageModelAndUploadDocumentAsync(searchClient, records, innerOptions, cancellationToken)).ConfigureAwait(false);

        // Get results
        var resultKeys = results.Value.Results.Select(x => x.Key).ToList();
        foreach (var resultKey in resultKeys) { yield return resultKey; }
    }

    /// <summary>
    /// Get the document with the given key and map it to the data model using the configured mapper type.
    /// </summary>
    /// <param name="searchClient">The search client to use when fetching the document.</param>
    /// <param name="key">The key of the record to get.</param>
    /// <param name="innerOptions">The azure ai search sdk options for geting a document.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The retrieved document, mapped to the consumer data model.</returns>
    private async Task<TDataModel> GetDocumentAndMapToDataModelAsync(SearchClient searchClient, string key, Azure.Search.Documents.GetDocumentOptions innerOptions, CancellationToken cancellationToken = default)
    {
        if (this._options.MapperType == AzureAISearchVectorDBRecordMapperType.JsonObjectCustomerMapper)
        {
            var jsonObject = await searchClient.GetDocumentAsync<JsonObject>(key, innerOptions, cancellationToken).ConfigureAwait(false);
            return this._options.JsonObjectCustomMapper!.MapFromStorageToDataModel(jsonObject);
        }

        return await searchClient.GetDocumentAsync<TDataModel>(key, innerOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Map the data model to the storage model and upload the document.
    /// </summary>
    /// <param name="searchClient">The search client to use when uploading the document.</param>
    /// <param name="records">The records to upload.</param>
    /// <param name="innerOptions">The azure ai search sdk options for uploading a document.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The document upload result.</returns>
    private async Task<Response<IndexDocumentsResult>> MapToStorageModelAndUploadDocumentAsync(SearchClient searchClient, IEnumerable<TDataModel> records, IndexDocumentsOptions innerOptions, CancellationToken cancellationToken = default)
    {
        if (this._options.MapperType == AzureAISearchVectorDBRecordMapperType.JsonObjectCustomerMapper)
        {
            var jsonObjects = records.Select(this._options.JsonObjectCustomMapper!.MapFromDataToStorageModel);
            return await searchClient.UploadDocumentsAsync<JsonObject>(jsonObjects, innerOptions, cancellationToken).ConfigureAwait(false);
        }

        return await searchClient.UploadDocumentsAsync<TDataModel>(records, innerOptions, cancellationToken).ConfigureAwait(false);
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
