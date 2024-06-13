// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Memory;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Microsoft.SemanticKernel.Connectors.Qdrant;

/// <summary>
/// Service for storing and retrieving records, that uses Qdrant as the underlying storage.
/// </summary>
/// <typeparam name="TRecord">The data model to use for adding, updating and retrieving data from storage.</typeparam>
public sealed class QdrantVectorRecordStore<TRecord> : IVectorRecordStore<ulong, TRecord>, IVectorRecordStore<Guid, TRecord>
    where TRecord : class
{
    /// <summary>Qdrant client that can be used to manage the collections and points in a Qdrant store.</summary>
    private readonly QdrantClient _qdrantClient;

    /// <summary>Optional configuration options for this class.</summary>
    private readonly QdrantVectorRecordStoreOptions<TRecord> _options;

    /// <summary>A mapper to use for converting between qdrant point and consumer models.</summary>
    private readonly IVectorStoreRecordMapper<TRecord, PointStruct> _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="QdrantVectorRecordStore{TRecord}"/> class.
    /// </summary>
    /// <param name="qdrantClient">Qdrant client that can be used to manage the collections and points in a Qdrant store.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public QdrantVectorRecordStore(QdrantClient qdrantClient, QdrantVectorRecordStoreOptions<TRecord>? options = null)
    {
        // Verify.
        Verify.NotNull(qdrantClient);

        // Assign.
        this._qdrantClient = qdrantClient;
        this._options = options ?? new QdrantVectorRecordStoreOptions<TRecord>();

        // Assign Mapper.
        if (this._options.MapperType == QdrantRecordMapperType.QdrantPointStructCustomMapper)
        {
            if (this._options.PointStructCustomMapper is null)
            {
                throw new ArgumentException($"The {nameof(QdrantVectorRecordStoreOptions<TRecord>.PointStructCustomMapper)} option needs to be set if a {nameof(QdrantVectorRecordStoreOptions<TRecord>.MapperType)} of {nameof(QdrantRecordMapperType.QdrantPointStructCustomMapper)} has been chosen.", nameof(options));
            }

            this._mapper = this._options.PointStructCustomMapper;
        }
        else
        {
            this._mapper = new QdrantVectorStoreRecordJsonMapper<TRecord>(new QdrantVectorStoreRecordJsonMapperOptions { HasNamedVectors = this._options.HasNamedVectors });
        }
    }

    /// <inheritdoc />
    public async Task<TRecord> GetAsync(ulong key, GetRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(key);

        var retrievedPoints = await this.GetBatchAsync([key], options, cancellationToken).ToListAsync(cancellationToken).ConfigureAwait(false);
        return retrievedPoints[0];
    }

    /// <inheritdoc />
    public async Task<TRecord> GetAsync(Guid key, GetRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(key);

        var retrievedPoints = await this.GetBatchAsync([key], options, cancellationToken).ToListAsync(cancellationToken).ConfigureAwait(false);
        return retrievedPoints[0];
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TRecord> GetBatchAsync(IEnumerable<ulong> keys, GetRecordOptions? options = default, CancellationToken cancellationToken = default)
    {
        return this.GetBatchByPointIdAsync(keys, key => new PointId { Num = key }, options, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TRecord> GetBatchAsync(IEnumerable<Guid> keys, GetRecordOptions? options = default, CancellationToken cancellationToken = default)
    {
        return this.GetBatchByPointIdAsync(keys, key => new PointId { Uuid = key.ToString("D") }, options, cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteAsync(ulong key, DeleteRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(key);

        var collectionName = this.ChooseCollectionName(options?.CollectionName);
        return this._qdrantClient.DeleteAsync(
            collectionName,
            key,
            wait: true,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid key, DeleteRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(key);

        var collectionName = this.ChooseCollectionName(options?.CollectionName);
        return this._qdrantClient.DeleteAsync(
            collectionName,
            key,
            wait: true,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteBatchAsync(IEnumerable<ulong> keys, DeleteRecordOptions? options = default, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(keys);

        var collectionName = this.ChooseCollectionName(options?.CollectionName);
        return this._qdrantClient.DeleteAsync(
            collectionName,
            keys.ToList(),
            wait: true,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteBatchAsync(IEnumerable<Guid> keys, DeleteRecordOptions? options = default, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(keys);

        var collectionName = this.ChooseCollectionName(options?.CollectionName);
        return this._qdrantClient.DeleteAsync(
            collectionName,
            keys.ToList(),
            wait: true,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ulong> UpsertAsync(TRecord record, UpsertRecordOptions? options = default, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(record);

        // Create options.
        var collectionName = this.ChooseCollectionName(options?.CollectionName);

        // Create point from record.
        var pointStruct = this._mapper.MapFromDataToStorageModel(record);

        // Upsert.
        await this._qdrantClient.UpsertAsync(collectionName, [pointStruct], true, cancellationToken: cancellationToken).ConfigureAwait(false);
        return pointStruct.Id.Num;
    }

    /// <inheritdoc />
    async Task<Guid> IVectorRecordStore<Guid, TRecord>.UpsertAsync(TRecord record, UpsertRecordOptions? options, CancellationToken cancellationToken)
    {
        Verify.NotNull(record);

        // Create options.
        var collectionName = this.ChooseCollectionName(options?.CollectionName);

        // Create point from record.
        var pointStruct = this._mapper.MapFromDataToStorageModel(record);

        // Upsert.
        await this._qdrantClient.UpsertAsync(collectionName, [pointStruct], true, cancellationToken: cancellationToken).ConfigureAwait(false);
        return Guid.Parse(pointStruct.Id.Uuid);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ulong> UpsertBatchAsync(IEnumerable<TRecord> records, UpsertRecordOptions? options = default, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Verify.NotNull(records);

        // Create Options
        var collectionName = this.ChooseCollectionName(options?.CollectionName);

        // Create points from records.
        var pointStructs = records.Select(this._mapper.MapFromDataToStorageModel).ToList();

        // Upsert.
        await this._qdrantClient.UpsertAsync(collectionName, pointStructs, true, cancellationToken: cancellationToken).ConfigureAwait(false);
        foreach (var pointStruct in pointStructs)
        {
            yield return pointStruct.Id.Num;
        }
    }

    /// <inheritdoc />
    async IAsyncEnumerable<Guid> IVectorRecordStore<Guid, TRecord>.UpsertBatchAsync(IEnumerable<TRecord> records, UpsertRecordOptions? options, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Verify.NotNull(records);

        // Create Options
        var collectionName = this.ChooseCollectionName(options?.CollectionName);

        // Create points from records.
        var pointStructs = records.Select(this._mapper.MapFromDataToStorageModel).ToList();

        // Upsert.
        await this._qdrantClient.UpsertAsync(collectionName, pointStructs, true, cancellationToken: cancellationToken).ConfigureAwait(false);
        foreach (var pointStruct in pointStructs)
        {
            yield return Guid.Parse(pointStruct.Id.Uuid);
        }
    }

    /// <summary>
    /// Get the requested records from the Qdrant store using the provided keys.
    /// </summary>
    /// <param name="keys">The keys of the points to retrieve.</param>
    /// <param name="keyConverter">Function to convert the provided keys to point ids.</param>
    /// <param name="options">The retrieval options.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The retrieved points.</returns>
    private async IAsyncEnumerable<TRecord> GetBatchByPointIdAsync<TKey>(
        IEnumerable<TKey> keys,
        Func<TKey, PointId> keyConverter,
        GetRecordOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Verify.NotNull(keys);

        // Create options.
        var collectionName = this.ChooseCollectionName(options?.CollectionName);
        var pointsIds = keys.Select(key => keyConverter(key)).ToArray();

        // Retrieve data points.
        var retrievedPoints = await this._qdrantClient.RetrieveAsync(collectionName, pointsIds, true, options?.IncludeVectors ?? false, cancellationToken: cancellationToken).ConfigureAwait(false);

        // Check that we found the required number of values.
        if (retrievedPoints.Count != pointsIds.Length)
        {
            throw new HttpOperationException(HttpStatusCode.NotFound, null, null, null);
        }

        // Convert the retrieved points to the target data model.
        foreach (var retrievedPoint in retrievedPoints)
        {
            var pointStruct = new PointStruct
            {
                Id = retrievedPoint.Id,
                Vectors = retrievedPoint.Vectors,
                Payload = { }
            };

            foreach (KeyValuePair<string, Value> payloadEntry in retrievedPoint.Payload)
            {
                pointStruct.Payload.Add(payloadEntry.Key, payloadEntry.Value);
            }

            yield return this._mapper.MapFromStorageToDataModel(pointStruct, options);
        }
    }

    /// <summary>
    /// Choose the right collection name to use for the operation by using the one provided
    /// as part of the operation options, or the default one provided at construction time.
    /// </summary>
    /// <param name="operationCollectionName">The collection name provided on the operation options.</param>
    /// <returns>The collection name to use.</returns>
    private string ChooseCollectionName(string? operationCollectionName)
    {
        var collectionName = operationCollectionName ?? this._options.DefaultCollectionName;
        if (collectionName is null)
        {
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
            throw new ArgumentException("Collection name must be provided in the operation options, since no default was provided at construction time.", "options");
#pragma warning restore CA2208 // Instantiate argument exceptions correctly
        }

        return collectionName;
    }
}
