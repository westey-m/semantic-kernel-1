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
public sealed class QdrantVectorCollectionStore : IVectorCollectionStore, IConfiguredVectorCollectionStore, IVectorStore
{
    /// <summary>Qdrant client that can be used to manage the collections and points in a Qdrant store.</summary>
    private readonly QdrantClient _qdrantClient;

    /// <summary>Used to create new collections in the vector store using configuration on the constructor.</summary>
    private readonly IVectorCollectionCreate? _vectorCollectionCreate;

    /// <summary>Used to create new collections in the vector store using configuration on the method.</summary>
    private readonly IConfiguredVectorCollectionCreate? _configuredVectorCollectionCreate;

    /// <summary>Optional factory used to construct vector store instances, for cases where options need to be customized.</summary>
    private readonly IQdrantVectorRecordStoreFactory? _recordStoreFactory;

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
    /// <param name="recordStoreFactory">An optional factory to use for constructing <see cref="QdrantVectorRecordStore{TRecord}"/> instances, if custom options are required.</param>
    public QdrantVectorCollectionStore(QdrantClient qdrantClient, IConfiguredVectorCollectionCreate configuredVectorCollectionCreate, IQdrantVectorRecordStoreFactory? recordStoreFactory = default)
    {
        Verify.NotNull(qdrantClient);
        Verify.NotNull(configuredVectorCollectionCreate);

        this._qdrantClient = qdrantClient;
        this._configuredVectorCollectionCreate = configuredVectorCollectionCreate;
        this._recordStoreFactory = recordStoreFactory;
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
    public IVectorRecordStore<TKey, TRecord> GetCollection<TKey, TRecord>(string name, VectorStoreRecordDefinition? vectorStoreRecordDefinition = null) where TRecord : class
    {
        if (typeof(TKey) != typeof(ulong) && typeof(TKey) != typeof(Guid))
        {
            throw new NotSupportedException("Only ulong and Guid keys are supported.");
        }

        if (this._recordStoreFactory is not null)
        {
            return this._recordStoreFactory.CreateRecordStore<TKey, TRecord>(this._qdrantClient, name, vectorStoreRecordDefinition);
        }

        var directlyCreatedStore = new QdrantVectorRecordStore<TRecord>(this._qdrantClient, name, new QdrantVectorRecordStoreOptions<TRecord>() { VectorStoreRecordDefinition = vectorStoreRecordDefinition }) as IVectorRecordStore<TKey, TRecord>;
        return directlyCreatedStore!;
    }

    /// <inheritdoc />
    public async Task<IVectorRecordStore<TKey, TRecord>> CreateCollectionAsync<TKey, TRecord>(string name, VectorStoreRecordDefinition? vectorStoreRecordDefinition = null, CancellationToken cancellationToken = default) where TRecord : class
    {
        if (typeof(TKey) != typeof(ulong) && typeof(TKey) != typeof(Guid))
        {
            throw new NotSupportedException("Only ulong and Guid keys are supported.");
        }

        if (vectorStoreRecordDefinition is null)
        {
            vectorStoreRecordDefinition = VectorStoreRecordPropertyReader.CreateVectorStoreRecordDefinitionFromType(typeof(TRecord), true);
        }

        await this.CreateCollectionAsync(name, vectorStoreRecordDefinition, cancellationToken).ConfigureAwait(false);
        return this.GetCollection<TKey, TRecord>(name, vectorStoreRecordDefinition);
    }

    /// <inheritdoc />
    public async Task<IVectorRecordStore<TKey, TRecord>> CreateCollectionIfNotExistsAsync<TKey, TRecord>(string name, VectorStoreRecordDefinition? vectorStoreRecordDefinition = null, CancellationToken cancellationToken = default) where TRecord : class
    {
        if (typeof(TKey) != typeof(ulong) && typeof(TKey) != typeof(Guid))
        {
            throw new NotSupportedException("Only ulong and Guid keys are supported.");
        }

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
