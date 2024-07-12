// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Data;
using NRedisStack.RedisStackCommands;
using NRedisStack.Search.Literals.Enums;
using NRedisStack.Search;
using StackExchange.Redis;

namespace Microsoft.SemanticKernel.Connectors.Redis;

/// <summary>
/// Provides collection retrieval and deletion for Redis.
/// </summary>
public sealed class RedisVectorStore : IVectorStore
{
    /// <summary>The redis database to read/write indices from.</summary>
    private readonly IDatabase _database;

    /// <summary>Optional configuration options for this class.</summary>
    private readonly RedisVectorStoreOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisVectorStore"/> class.
    /// </summary>
    /// <param name="database">The redis database to read/write indices from.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    public RedisVectorStore(IDatabase database, RedisVectorStoreOptions? options = default)
    {
        Verify.NotNull(database);

        this._database = database;
        this._options = options ?? new RedisVectorStoreOptions();
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
            return this._options.VectorStoreCollectionFactory.CreateVectorStoreCollection<TKey, TRecord>(this._database, name, vectorStoreRecordDefinition);
        }

        if (this._options.StorageType == RedisStorageType.Hashes)
        {
            var directlyCreatedStore = new RedisHashSetVectorRecordStore<TRecord>(this._database, name, new RedisHashSetVectorRecordStoreOptions<TRecord>() { VectorStoreRecordDefinition = vectorStoreRecordDefinition }) as IVectorRecordStore<TKey, TRecord>;
            return directlyCreatedStore!;
        }
        else
        {
            var directlyCreatedStore = new RedisVectorRecordStore<TRecord>(this._database, name, new RedisVectorRecordStoreOptions<TRecord>() { VectorStoreRecordDefinition = vectorStoreRecordDefinition }) as IVectorRecordStore<TKey, TRecord>;
            return directlyCreatedStore!;
        }
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

        var schema = new Schema();

        // Loop through all properties and create the index fields.
        foreach (var property in vectorStoreRecordDefinition.Properties)
        {
            // Key property.
            if (property is VectorStoreRecordKeyProperty keyProperty)
            {
                // Do nothing, since key is not stored as part of the payload and therefore doesn't have to be added to the index.
            }

            // Data property.
            if (property is VectorStoreRecordDataProperty dataProperty && dataProperty.IsFilterable)
            {
                if (dataProperty.PropertyType is null)
                {
                    throw new InvalidOperationException($"Property {nameof(dataProperty.PropertyType)} on {nameof(VectorStoreRecordDataProperty)} '{dataProperty.PropertyName}' must be set to create a collection, since the property is filterable.");
                }

                if (dataProperty.PropertyType == typeof(string))
                {
                    schema.AddTextField(new FieldName($"$.{dataProperty.PropertyName}", dataProperty.PropertyName));
                }

                if (RedisVectorStoreCollectionCreateMapping.s_supportedFilterableNumericDataTypes.Contains(dataProperty.PropertyType))
                {
                    schema.AddNumericField(new FieldName($"$.{dataProperty.PropertyName}", dataProperty.PropertyName));
                }
            }

            // Vector property.
            if (property is VectorStoreRecordVectorProperty vectorProperty)
            {
                if (vectorProperty.Dimensions is not > 0)
                {
                    throw new InvalidOperationException($"Property {nameof(vectorProperty.Dimensions)} on {nameof(VectorStoreRecordVectorProperty)} '{vectorProperty.PropertyName}' must be set to a positive ingeteger to create a collection.");
                }

                var indexKind = RedisVectorStoreCollectionCreateMapping.GetSDKIndexKind(vectorProperty);
                var distanceAlgorithm = RedisVectorStoreCollectionCreateMapping.GetSDKDistanceAlgorithm(vectorProperty);
                var dimensions = vectorProperty.Dimensions.Value.ToString(CultureInfo.InvariantCulture);
                schema.AddVectorField(new FieldName($"$.{vectorProperty.PropertyName}", vectorProperty.PropertyName), indexKind, new Dictionary<string, object>()
                {
                    ["TYPE"] = "FLOAT32",
                    ["DIM"] = dimensions,
                    ["DISTANCE_METRIC"] = distanceAlgorithm
                });
            }
        }

        // Create the index creation params.
        var createParams = new FTCreateParams()
            .AddPrefix($"{name}:");

        if (this._options.StorageType == RedisStorageType.Hashes)
        {
            createParams = createParams.On(IndexDataType.HASH);
        }

        if (this._options.StorageType == RedisStorageType.Json)
        {
            createParams = createParams.On(IndexDataType.JSON);
        }

        // Create the index.
        await this._database.FT().CreateAsync(name, createParams, schema).ConfigureAwait(false);

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
