// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Data;
using NRedisStack.RedisStackCommands;
using StackExchange.Redis;

namespace Microsoft.SemanticKernel.Connectors.Redis;

/// <summary>
/// Provides collection retrieval and deletion for Redis.
/// </summary>
public sealed class RedisVectorCollectionStore : IVectorCollectionStore, IConfiguredVectorCollectionStore, IVectorStore
{
    /// <summary>The redis database to read/write indices from.</summary>
    private readonly IDatabase _database;

    /// <summary>Used to create new collections in the vector store using configuration on the constructor.</summary>
    private readonly IVectorCollectionCreate? _vectorCollectionCreate;

    /// <summary>Used to create new collections in the vector store using configuration on the method.</summary>
    private readonly IConfiguredVectorCollectionCreate? _configuredVectorCollectionCreate;

    /// <summary>Optional factory used to construct vector store instances, for cases where options need to be customized.</summary>
    private readonly IRedisVectorRecordStoreFactory? _recordStoreFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisVectorCollectionStore"/> class.
    /// </summary>
    /// <param name="database">The redis database to read/write indices from.</param>
    /// <param name="vectorCollectionCreate">Used to create new collections in the vector store.</param>
    public RedisVectorCollectionStore(IDatabase database, IVectorCollectionCreate vectorCollectionCreate)
    {
        Verify.NotNull(database);
        Verify.NotNull(vectorCollectionCreate);

        this._database = database;
        this._vectorCollectionCreate = vectorCollectionCreate;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisVectorCollectionStore"/> class.
    /// </summary>
    /// <param name="database">The redis database to read/write indices from.</param>
    /// <param name="configuredVectorCollectionCreate">Used to create new collections in the vector store.</param>
    /// <param name="recordStoreFactory">An optional factory to use for constructing <see cref="RedisVectorRecordStore{TRecord}"/> instances, if custom options are required.</param>
    public RedisVectorCollectionStore(IDatabase database, IConfiguredVectorCollectionCreate configuredVectorCollectionCreate, IRedisVectorRecordStoreFactory? recordStoreFactory = default)
    {
        Verify.NotNull(database);
        Verify.NotNull(configuredVectorCollectionCreate);

        this._database = database;
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
        if (typeof(TKey) != typeof(string))
        {
            throw new NotSupportedException("Only string keys are supported.");
        }

        if (this._recordStoreFactory is not null)
        {
            var factoryCreatedStore = this._recordStoreFactory.CreateRecordStore<TRecord>(this._database, name, vectorStoreRecordDefinition) as IVectorRecordStore<TKey, TRecord>;
            return factoryCreatedStore!;
        }

        var directlyCreatedStore = new RedisVectorRecordStore<TRecord>(this._database) as IVectorRecordStore<TKey, TRecord>;
        return directlyCreatedStore!;
    }

    /// <inheritdoc />
    public async Task<IVectorRecordStore<TKey, TRecord>> CreateCollectionAsync<TKey, TRecord>(string name, VectorStoreRecordDefinition? vectorStoreRecordDefinition = null, CancellationToken cancellationToken = default) where TRecord : class
    {
        if (typeof(TKey) != typeof(string))
        {
            throw new NotSupportedException("Only string keys are supported.");
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
        if (typeof(TKey) != typeof(string))
        {
            throw new NotSupportedException("Only string keys are supported.");
        }

        if (!await this.CollectionExistsAsync(name, cancellationToken).ConfigureAwait(false))
        {
            return await this.CreateCollectionAsync<TKey, TRecord>(name, vectorStoreRecordDefinition, cancellationToken).ConfigureAwait(false);
        }

        return this.GetCollection<TKey, TRecord>(name, vectorStoreRecordDefinition);
    }

    /// <inheritdoc />
    public async Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            await this._database.FT().InfoAsync(name).ConfigureAwait(false);
            return true;
        }
        catch (RedisServerException ex) when (ex.Message.Contains("Unknown index name"))
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task DeleteCollectionAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            await this._database.FT().DropIndexAsync(name).ConfigureAwait(false);
        }
        catch (RedisServerException ex)
        {
            throw new HttpOperationException(ex.Message, ex);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> ListCollectionNamesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        RedisResult[] listResult;

        try
        {
            listResult = await this._database.FT()._ListAsync().ConfigureAwait(false);
        }
        catch (RedisServerException ex)
        {
            throw new HttpOperationException(ex.Message, ex);
        }

        foreach (var item in listResult)
        {
            var name = item.ToString();
            if (name != null)
            {
                yield return name;
            }
        }
    }
}
