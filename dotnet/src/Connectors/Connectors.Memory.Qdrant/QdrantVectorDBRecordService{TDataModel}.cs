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
/// Vector store that uses Qdrant as the underlying storage.
/// </summary>
/// <typeparam name="TDataModel">The data model to use for adding, updating and retrieving data from storage.</typeparam>
public class QdrantVectorDBRecordService<TDataModel> : IVectorDBRecordService<ulong, TDataModel>, IVectorDBRecordService<Guid, TDataModel>
    where TDataModel : class
{
    /// <summary>Qdrant client that can be used to manage the points in a Qdrant store.</summary>
    private readonly QdrantClient _qdrantClient;

    /// <summary>The name of the collection to use with this store if none is provided for any individual operation.</summary>
    private readonly string _defaultCollectionName;

    /// <summary>A mapper to use for converting between qdrant point and consumer models.</summary>
    private readonly IQdrantVectorDBRecordMapper<TDataModel> _recordMapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="QdrantVectorDBRecordService{TDataModel}"/> class.
    /// </summary>
    /// <param name="qdrantClient">Qdrant client that can be used to manage the points in a Qdrant store.</param>
    /// <param name="defaultCollectionName">The name of the collection to use with this store if none is provided for any individual operation.</param>
    /// <param name="recordMapper">A mapper to use for converting between qdrant point and consumer models.</param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public QdrantVectorDBRecordService(QdrantClient qdrantClient, string defaultCollectionName, IQdrantVectorDBRecordMapper<TDataModel> recordMapper)
    {
        // Verify.
        Verify.NotNull(qdrantClient);
        Verify.NotNullOrWhiteSpace(defaultCollectionName);
        Verify.NotNull(recordMapper);

        // Assign.
        this._qdrantClient = qdrantClient;
        this._defaultCollectionName = defaultCollectionName;
        this._recordMapper = recordMapper;
    }

    /// <inheritdoc />
    public async Task<TDataModel?> GetAsync(ulong key, GetRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(key);

        var retrievedPoints = await this.GetBatchAsync([key], options, cancellationToken).ToListAsync(cancellationToken).ConfigureAwait(false);
        return retrievedPoints[0];
    }

    /// <inheritdoc />
    public async Task<TDataModel?> GetAsync(Guid key, GetRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(key);

        var retrievedPoints = await this.GetBatchAsync([key], options, cancellationToken).ToListAsync(cancellationToken).ConfigureAwait(false);
        return retrievedPoints[0];
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TDataModel?> GetBatchAsync(IEnumerable<ulong> keys, GetRecordOptions? options = default, CancellationToken cancellationToken = default)
    {
        return this.GetBatchByPointIdAsync(keys, key => new PointId { Num = key }, options, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TDataModel?> GetBatchAsync(IEnumerable<Guid> keys, GetRecordOptions? options = default, CancellationToken cancellationToken = default)
    {
        return this.GetBatchByPointIdAsync(keys, key => new PointId { Uuid = key.ToString("D") }, options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ulong> RemoveAsync(ulong key, RemoveRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(key);

        var collectionName = options?.CollectionName ?? this._defaultCollectionName;
        await this._qdrantClient.DeleteAsync(
            collectionName,
            key,
            wait: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return key;
    }

    /// <inheritdoc />
    public async Task<Guid> RemoveAsync(Guid key, RemoveRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(key);

        var collectionName = options?.CollectionName ?? this._defaultCollectionName;
        await this._qdrantClient.DeleteAsync(
            collectionName,
            key,
            wait: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return key;
    }

    /// <inheritdoc />
    public async Task RemoveBatchAsync(IEnumerable<ulong> keys, RemoveRecordOptions? options = default, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(keys);

        var collectionName = options?.CollectionName ?? this._defaultCollectionName;
        await this._qdrantClient.DeleteAsync(
            collectionName,
            keys.ToList(),
            wait: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveBatchAsync(IEnumerable<Guid> keys, RemoveRecordOptions? options = default, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(keys);

        var collectionName = options?.CollectionName ?? this._defaultCollectionName;
        var result = await this._qdrantClient.DeleteAsync(
            collectionName,
            keys.ToList(),
            wait: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ulong> UpsertAsync(TDataModel record, UpsertRecordOptions? options = default, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(record);

        // Create options.
        var collectionName = options?.CollectionName ?? this._defaultCollectionName;

        // Create point from record.
        var pointStruct = this._recordMapper.MapFromDataModelToGrpc(record);

        // Upsert.
        await this._qdrantClient.UpsertAsync(collectionName, [pointStruct], true, cancellationToken: cancellationToken).ConfigureAwait(false);
        return pointStruct.Id.Num;
    }

    /// <inheritdoc />
    async Task<Guid> IVectorDBRecordService<Guid, TDataModel>.UpsertAsync(TDataModel record, UpsertRecordOptions? options, CancellationToken cancellationToken)
    {
        Verify.NotNull(record);

        // Create options.
        var collectionName = options?.CollectionName ?? this._defaultCollectionName;

        // Create point from record.
        var pointStruct = this._recordMapper.MapFromDataModelToGrpc(record);

        // Upsert.
        await this._qdrantClient.UpsertAsync(collectionName, [pointStruct], true, cancellationToken: cancellationToken).ConfigureAwait(false);
        return Guid.Parse(pointStruct.Id.Uuid);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ulong> UpsertBatchAsync(IEnumerable<TDataModel> records, UpsertRecordOptions? options = default, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Verify.NotNull(records);

        // Create Options
        var collectionName = options?.CollectionName ?? this._defaultCollectionName;

        // Create points from records.
        var pointStructs = records.Select(this._recordMapper.MapFromDataModelToGrpc).ToList();

        // Upsert.
        await this._qdrantClient.UpsertAsync(collectionName, pointStructs, true, cancellationToken: cancellationToken).ConfigureAwait(false);
        foreach (var pointStruct in pointStructs)
        {
            yield return pointStruct.Id.Num;
        }
    }

    /// <inheritdoc />
    async IAsyncEnumerable<Guid> IVectorDBRecordService<Guid, TDataModel>.UpsertBatchAsync(IEnumerable<TDataModel> records, UpsertRecordOptions? options, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Verify.NotNull(records);

        // Create Options
        var collectionName = options?.CollectionName ?? this._defaultCollectionName;

        // Create points from records.
        var pointStructs = records.Select(this._recordMapper.MapFromDataModelToGrpc).ToList();

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
    private async IAsyncEnumerable<TDataModel?> GetBatchByPointIdAsync<TKey>(
        IEnumerable<TKey> keys,
        Func<TKey, PointId> keyConverter,
        GetRecordOptions? options = default,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Verify.NotNull(keys);

        // Create options.
        var collectionName = options?.CollectionName ?? this._defaultCollectionName;
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
            yield return this._recordMapper.MapFromGrpcToDataModel(retrievedPoint, options);
        }
    }
}
