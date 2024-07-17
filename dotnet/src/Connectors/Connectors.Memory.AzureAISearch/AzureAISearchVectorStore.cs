// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Search.Documents.Indexes;
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
    public IVectorStoreRecordCollection<TKey, TRecord> GetCollection<TKey, TRecord>(string name, VectorStoreRecordDefinition? vectorStoreRecordDefinition = null) where TRecord : class
    {
        if (typeof(TKey) != typeof(string))
        {
            throw new NotSupportedException("Only string keys are supported.");
        }

        if (this._options.VectorStoreCollectionFactory is not null)
        {
            return this._options.VectorStoreCollectionFactory.CreateVectorStoreRecordCollection<TKey, TRecord>(this._searchIndexClient, name, vectorStoreRecordDefinition);
        }

        var directlyCreatedStore = new AzureAISearchVectorStoreRecordCollection<TRecord>(this._searchIndexClient, name, new AzureAISearchVectorStoreRecordCollectionOptions<TRecord>() { VectorStoreRecordDefinition = vectorStoreRecordDefinition }) as IVectorStoreRecordCollection<TKey, TRecord>;
        return directlyCreatedStore!;
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
