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
/// Provides collection retrieval and deletion for Azure AI Search.
/// </summary>
public sealed class AzureAISearchVectorCollectionStore : IVectorCollectionStore, IConfiguredVectorCollectionStore
{
    /// <summary>Azure AI Search client that can be used to manage the list of indices in an Azure AI Search Service.</summary>
    private readonly SearchIndexClient _searchIndexClient;

    /// <summary>Used to create new collections in the vector store using configuration on the constructor.</summary>
    private readonly IVectorCollectionCreate? _vectorCollectionCreate;

    /// <summary>Used to create new collections in the vector store using configuration on the method.</summary>
    private readonly IConfiguredVectorCollectionCreate? _configuredVectorCollectionCreate;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureAISearchVectorCollectionStore"/> class.
    /// </summary>
    /// <param name="searchIndexClient">Azure AI Search client that can be used to manage the list of indices in an Azure AI Search Service.</param>
    /// <param name="vectorCollectionCreate">Used to create new collections in the vector store.</param>
    public AzureAISearchVectorCollectionStore(SearchIndexClient searchIndexClient, IVectorCollectionCreate vectorCollectionCreate)
    {
        Verify.NotNull(searchIndexClient);
        Verify.NotNull(vectorCollectionCreate);

        this._searchIndexClient = searchIndexClient;
        this._vectorCollectionCreate = vectorCollectionCreate;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureAISearchVectorCollectionStore"/> class.
    /// </summary>
    /// <param name="searchIndexClient">Azure AI Search client that can be used to manage the list of indices in an Azure AI Search Service.</param>
    /// <param name="configuredVectorCollectionCreate">Used to create new collections in the vector store.</param>
    public AzureAISearchVectorCollectionStore(SearchIndexClient searchIndexClient, IConfiguredVectorCollectionCreate configuredVectorCollectionCreate)
    {
        Verify.NotNull(searchIndexClient);
        Verify.NotNull(configuredVectorCollectionCreate);

        this._searchIndexClient = searchIndexClient;
        this._configuredVectorCollectionCreate = configuredVectorCollectionCreate;
    }

    /// <inheritdoc />
    public Task CreateCollectionAsync(string name, CancellationToken cancellationToken = default)
    {
        if (this._vectorCollectionCreate is null)
        {
            throw new InvalidOperationException($"Cannot create a collection without a {nameof(IVectorCollectionCreate)}.");
        }

        return this._vectorCollectionCreate.CreateCollectionAsync(name, cancellationToken);
    }

    /// <inheritdoc />
    public Task CreateCollectionAsync(string name, VectorStoreRecordDefinition vectorStoreRecordDefinition, CancellationToken cancellationToken = default)
    {
        if (this._configuredVectorCollectionCreate is null)
        {
            throw new InvalidOperationException($"Cannot create a collection without a {nameof(IConfiguredVectorCollectionCreate)}.");
        }

        return this._configuredVectorCollectionCreate.CreateCollectionAsync(name, vectorStoreRecordDefinition, cancellationToken);
    }

    /// <inheritdoc />
    public Task CreateCollectionAsync<TRecord>(string name, CancellationToken cancellationToken = default)
    {
        if (this._configuredVectorCollectionCreate is null)
        {
            throw new InvalidOperationException($"Cannot create a collection without a {nameof(IConfiguredVectorCollectionCreate)}.");
        }

        return this._configuredVectorCollectionCreate.CreateCollectionAsync<TRecord>(name, cancellationToken);
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
