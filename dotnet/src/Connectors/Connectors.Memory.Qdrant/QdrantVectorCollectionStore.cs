// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.SemanticKernel.Data;
using Qdrant.Client;

namespace Microsoft.SemanticKernel.Connectors.Qdrant;

/// <summary>
/// Provides collection retrieval and deletion for Qdrant.
/// </summary>
public sealed class QdrantVectorCollectionStore : IVectorCollectionStore, IConfiguredVectorCollectionStore
{
    /// <summary>Qdrant client that can be used to manage the collections and points in a Qdrant store.</summary>
    private readonly QdrantClient _qdrantClient;

    /// <summary>Used to create new collections in the vector store using configuration on the constructor.</summary>
    private readonly IVectorCollectionCreate? _vectorCollectionCreate;

    /// <summary>Used to create new collections in the vector store using configuration on the method.</summary>
    private readonly IConfiguredVectorCollectionCreate? _configuredVectorCollectionCreate;

    /// <summary>
    /// Initializes a new instance of the <see cref="QdrantVectorCollectionStore"/> class.
    /// </summary>
    /// <param name="qdrantClient">Qdrant client that can be used to manage the collections and points in a Qdrant store.</param>
    /// <param name="vectorCollectionCreate">Used to create new collections in the vector store.</param>
    public QdrantVectorCollectionStore(QdrantClient qdrantClient, IVectorCollectionCreate vectorCollectionCreate)
    {
        Verify.NotNull(qdrantClient);
        Verify.NotNull(vectorCollectionCreate);

        this._qdrantClient = qdrantClient;
        this._vectorCollectionCreate = vectorCollectionCreate;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QdrantVectorCollectionStore"/> class.
    /// </summary>
    /// <param name="qdrantClient">Qdrant client that can be used to manage the collections and points in a Qdrant store.</param>
    /// <param name="configuredVectorCollectionCreate">Used to create new collections in the vector store.</param>
    public QdrantVectorCollectionStore(QdrantClient qdrantClient, IConfiguredVectorCollectionCreate configuredVectorCollectionCreate)
    {
        Verify.NotNull(qdrantClient);
        Verify.NotNull(configuredVectorCollectionCreate);

        this._qdrantClient = qdrantClient;
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
            await this._qdrantClient.DeleteCollectionAsync(name, null, cancellationToken).ConfigureAwait(false);
        }
        catch (RpcException ex)
        {
            throw new HttpOperationException(ex.Message, ex);
        }
    }

    /// <inheritdoc />
    public async Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            return await this._qdrantClient.CollectionExistsAsync(name, cancellationToken).ConfigureAwait(false);
        }
        catch (RpcException ex)
        {
            throw new HttpOperationException(ex.Message, ex);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> ListCollectionNamesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> collections;

        try
        {
            collections = await this._qdrantClient.ListCollectionsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (RpcException ex)
        {
            throw new HttpOperationException(ex.Message, ex);
        }

        foreach (var collection in collections)
        {
            yield return collection;
        }
    }
}
