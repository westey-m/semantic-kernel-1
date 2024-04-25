// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>Optional configuration options for this class.</summary>
    private readonly AzureAISearchVectorStoreOptions? _options;

    /// <summary>
    /// Create a new instance of vector storage using Azure AI Search.
    /// </summary>
    /// <param name="searchClient">Azure AI Search client that can be used to manage data in a Azure AI Search Service index.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    public AzureAISearchVectorStore(SearchClient searchClient, AzureAISearchVectorStoreOptions? options = default)
    {
        this._client = searchClient ?? throw new ArgumentNullException(nameof(searchClient));
        this._options = options ?? throw new ArgumentNullException(nameof(options));

        if (this._options.MaxDegreeOfGetParallelism is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "AzureAISearchVectorStoreOptions.MaxDegreeOfGetParallelism must be greater than 0.");
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

        // Get data in batches
        var maxDegreeOfGetParallelism = this._options?.MaxDegreeOfGetParallelism ?? 50;
        var batch = keys.Take(maxDegreeOfGetParallelism);
        while (batch.Any())
        {
            // Get data
            var tasks = batch.Select(key => RunOperationAsync(() => this._client.GetDocumentAsync<TDataModel>(key, innerOptions, cancellationToken)));
            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            foreach (var result in results) { yield return result; }

            // Get next batch
            batch = keys.Take(maxDegreeOfGetParallelism);
        }
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
