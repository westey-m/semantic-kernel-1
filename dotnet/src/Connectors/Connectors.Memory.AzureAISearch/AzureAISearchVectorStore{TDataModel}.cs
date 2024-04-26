// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.SemanticKernel.Memory;

namespace Microsoft.SemanticKernel.Connectors.AzureAISearch;

/// <summary>
/// Vector store that uses Azure AI Search as the underlying storage.
/// </summary>
/// <typeparam name="TDataModel">The data model to use for adding, updating and retrieving data from storage.</typeparam>
public class AzureAISearchVectorStore<TDataModel> : IVectorStore<TDataModel>
{
    /// <summary>Azure AI Search client that can be used to manage data in a Azure AI Search Service index.</summary>
    private readonly SearchClient _client;

    /// <summary>The name of the key field for the collection that this class is used with.</summary>
    private readonly string _keyFieldName;

    /// <summary>Optional configuration options for this class.</summary>
    private readonly AzureAISearchVectorStoreOptions? _options;

    /// <summary>A property info object that points at the key field for the current model, allowing easy reading and writing of this property.</summary>
    private readonly PropertyInfo _keyFieldPropertyInfo;

    /// <summary>
    /// Create a new instance of vector storage using Azure AI Search.
    /// </summary>
    /// <param name="searchClient">Azure AI Search client that can be used to manage data in a Azure AI Search Service index.</param>
    /// <param name="keyFieldName">The name of the key field for the collection that this class is used with.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    public AzureAISearchVectorStore(SearchClient searchClient, string keyFieldName, AzureAISearchVectorStoreOptions? options = default)
    {
        this._client = searchClient ?? throw new ArgumentNullException(nameof(searchClient));
        this._keyFieldName = string.IsNullOrWhiteSpace(keyFieldName) ? throw new ArgumentException("Key Field name is required.", nameof(keyFieldName)) : keyFieldName;
        this._options = options ?? new AzureAISearchVectorStoreOptions();

        if (this._options.MaxDegreeOfGetParallelism is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "AzureAISearchVectorStoreOptions.MaxDegreeOfGetParallelism must be greater than 0.");
        }

        this._keyFieldPropertyInfo = typeof(TDataModel).GetProperty(this._keyFieldName, BindingFlags.Public | BindingFlags.Instance);
        if (this._keyFieldPropertyInfo.PropertyType != typeof(string))
        {
            throw new ArgumentException($"Key field must be of type string. Type of {this._keyFieldName} is {this._keyFieldPropertyInfo.PropertyType.FullName}.");
        }
    }

    /// <inheritdoc />
    public async Task<string> UpsertAsync(TDataModel record, CancellationToken cancellationToken = default)
    {
        if (record is null)
        {
            throw new ArgumentNullException(nameof(record));
        }

        var results = await RunOperationAsync(() => this._client.UploadDocumentsAsync<TDataModel>([record], new IndexDocumentsOptions(), cancellationToken)).ConfigureAwait(false);
        return results.Value.Results[0].Key;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> UpsertBatchAsync(IEnumerable<TDataModel> records, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (records is null)
        {
            throw new ArgumentNullException(nameof(records));
        }

        // Create Options
        var innerOptions = new IndexDocumentsOptions { ThrowOnAnyError = true };

        // Upload data
        var results = await RunOperationAsync(
            () => this._client.IndexDocumentsAsync(
                IndexDocumentsBatch.Upload(records),
                innerOptions,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        // Get results
        var resultKeys = results.Value.Results.Select(x => x.Key).ToList();
        foreach (var resultKey in resultKeys) { yield return resultKey; }
    }

    /// <inheritdoc />
    public async Task<TDataModel?> GetAsync(string key, VectorStoreGetDocumentOptions? options = default, CancellationToken cancellationToken = default)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        // Create Options
        var innerOptions = ConvertGetDocumentOptions(options);

        // Get data
        return await RunOperationAsync(() => this._client.GetDocumentAsync<TDataModel>(key, innerOptions, cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TDataModel> GetBatchAsync(IEnumerable<string> keys, VectorStoreGetDocumentOptions? options = default, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (keys is null)
        {
            throw new ArgumentNullException(nameof(keys));
        }

        // Create Options
        var innerOptions = ConvertGetDocumentOptions(options);

        // Split keys into batches
        var maxDegreeOfGetParallelism = this._options?.MaxDegreeOfGetParallelism ?? 50;
        var batches = keys.SplitIntoBatches(maxDegreeOfGetParallelism);

        foreach (var batch in batches)
        {
            // Get each batch
            var tasks = batch.Select(key => RunOperationAsync(() => this._client.GetDocumentAsync<TDataModel>(key, innerOptions, cancellationToken)));
            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            foreach (var result in results) { yield return result; }
        }
    }

    /// <inheritdoc />
    public async Task<string> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        var results = await RunOperationAsync(() => this._client.DeleteDocumentsAsync(this._keyFieldName, [key], new IndexDocumentsOptions(), cancellationToken)).ConfigureAwait(false);
        return results.Value.Results[0].Key;
    }

    /// <inheritdoc />
    public async Task RemoveBatchAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        if (keys is null)
        {
            throw new ArgumentNullException(nameof(keys));
        }

        var results = await RunOperationAsync(() => this._client.DeleteDocumentsAsync(this._keyFieldName, keys, new IndexDocumentsOptions(), cancellationToken)).ConfigureAwait(false);
    }

    private void EncodeKeyField(TDataModel record)
    {
        if (this._options?.RecordKeyEncoder is not null)
        {
            var key = this.GetKeyFieldValue(record);
            var encodedKey = this._options.RecordKeyEncoder.Invoke(key);
            this.SetKeyFieldValue(record, encodedKey);
        }
    }

    private void DecodeKeyField(TDataModel record)
    {
        if (this._options?.RecordKeyDecoder is not null)
        {
            var key = this.GetKeyFieldValue(record);
            var decodedKey = this._options.RecordKeyDecoder.Invoke(key);
            this.SetKeyFieldValue(record, decodedKey);
        }
    }

    /// <summary>
    /// Get the key property value from the given record.
    /// </summary>
    /// <param name="record">The record to read the key property's value from.</param>
    /// <returns>The value of the key property.</returns>
    private string GetKeyFieldValue(TDataModel record)
    {
        return (string)this._keyFieldPropertyInfo.GetValue(record);
    }

    /// <summary>
    /// Set the key property on the given record to the given value.
    /// </summary>
    /// <param name="record">The record to update.</param>
    /// <param name="value">The new value for the key property.</param>
    private void SetKeyFieldValue(TDataModel record, string value)
    {
        this._keyFieldPropertyInfo.SetValue(record, value);
    }

    /// <summary>
    /// Convert the public <see cref="VectorStoreGetDocumentOptions"/> options model to the azure ai search <see cref="GetDocumentOptions"/> options model.
    /// </summary>
    /// <param name="options">The public options model.</param>
    /// <returns>The azure ai search options model.</returns>
    private static GetDocumentOptions ConvertGetDocumentOptions(VectorStoreGetDocumentOptions? options)
    {
        var innerOptions = new GetDocumentOptions();
        var selectedFields = options?.SelectedFields;
        if (selectedFields is not null)
        {
            innerOptions.SelectedFields.AddRange(selectedFields);
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
