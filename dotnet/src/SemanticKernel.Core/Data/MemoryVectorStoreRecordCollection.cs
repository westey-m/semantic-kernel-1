// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Memory;

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// Decorator class for <see cref="IVectorStoreRecordCollection{TKey, TRecord}"/> that allows the use of any <see cref="IVectorStoreRecordCollection{TKey, TRecord}"/> with mapping to and from <see cref="MemoryRecord"/>.
/// </summary>
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public class MemoryVectorStoreRecordCollection<TStorageKey, TStorageRecord> : IVectorStoreRecordCollection<string, MemoryRecord>
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
    where TStorageRecord : class
{
    /// <summary>The <see cref="IVectorStoreRecordCollection{TKey, TRecord}"/> that is decorated by this class.</summary>
    private readonly IVectorStoreRecordCollection<TStorageKey, TStorageRecord> _innnerVectorStoreRecordCollection;

    /// <summary>Function that is used to encode the record id before it is written to storage or used to retrieve a record.</summary>
    private readonly Func<string, TStorageKey> _recordKeyEncoder;

    /// <summary>Function that is used to decode the record id after it is retrieved from storage or after upserting.</summary>
    private readonly Func<TStorageKey, string> _recordKeyDecoder;

    /// <summary>A mapper to map from the <see cref="MemoryRecord"/> type to the internal storage model.</summary>
    private readonly IVectorStoreRecordMapper<MemoryRecord, TStorageRecord> _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryVectorStoreRecordCollection{TKey, TStorageRecord}"/> class.
    /// </summary>
    /// <param name="innnerVectorStoreRecordCollection">The <see cref="IVectorStoreRecordCollection{TKey, TRecord}"/> that is decorated by this class.</param>
    /// <param name="recordKeyEncoder">Function that is used to encode the record id before it is written to storage or used to retrieve a record.</param>
    /// <param name="recordKeyDecoder">Function that is used to decode the record id after it is retrieved from storage or after upserting.</param>
    /// <param name="mapper">A mapper to map from the <see cref="MemoryRecord"/> type to the internal storage model.</param>
    public MemoryVectorStoreRecordCollection(
        IVectorStoreRecordCollection<TStorageKey, TStorageRecord> innnerVectorStoreRecordCollection,
        Func<string, TStorageKey> recordKeyEncoder,
        Func<TStorageKey, string> recordKeyDecoder,
        IVectorStoreRecordMapper<MemoryRecord, TStorageRecord> mapper)
    {
        // Verify.
        Verify.NotNull(innnerVectorStoreRecordCollection);
        Verify.NotNull(recordKeyEncoder);
        Verify.NotNull(recordKeyDecoder);
        Verify.NotNull(mapper);

        // Assign.
        this._innnerVectorStoreRecordCollection = innnerVectorStoreRecordCollection;
        this._recordKeyEncoder = recordKeyEncoder;
        this._recordKeyDecoder = recordKeyDecoder;
        this._mapper = mapper;
    }

    /// <inheritdoc />
    public string CollectionName => this._innnerVectorStoreRecordCollection.CollectionName;

    /// <inheritdoc />
    public Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        return this._innnerVectorStoreRecordCollection.CollectionExistsAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteCollectionAsync(CancellationToken cancellationToken = default)
    {
        return this._innnerVectorStoreRecordCollection.DeleteCollectionAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MemoryRecord?> GetAsync(string key, GetRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        var result = await this._innnerVectorStoreRecordCollection.GetAsync(
            this._recordKeyEncoder(key),
            options,
            cancellationToken).ConfigureAwait(false);

        if (result == null)
        {
            return null;
        }

        return this._mapper.MapFromStorageToDataModel(result, new StorageToDataModelMapperOptions() { IncludeVectors = options?.IncludeVectors ?? false });
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<MemoryRecord> GetBatchAsync(IEnumerable<string> keys, GetRecordOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var encodedKeys = keys.Select(this._recordKeyEncoder);
        var results = this._innnerVectorStoreRecordCollection.GetBatchAsync(
            encodedKeys,
            options,
            cancellationToken);

        await foreach (var result in results.ConfigureAwait(false))
        {
            yield return this._mapper.MapFromStorageToDataModel(result, new StorageToDataModelMapperOptions() { IncludeVectors = options?.IncludeVectors ?? false });
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string key, DeleteRecordOptions? options = default, CancellationToken cancellationToken = default)
    {
        await this._innnerVectorStoreRecordCollection.DeleteAsync(
            this._recordKeyEncoder(key),
            options,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteBatchAsync(IEnumerable<string> keys, DeleteRecordOptions? options = default, CancellationToken cancellationToken = default)
    {
        await this._innnerVectorStoreRecordCollection.DeleteBatchAsync(
            keys.Select(this._recordKeyEncoder),
            options,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string> UpsertAsync(MemoryRecord record, UpsertRecordOptions? options = default, CancellationToken cancellationToken = default)
    {
        var result = await this._innnerVectorStoreRecordCollection.UpsertAsync(
            this._mapper.MapFromDataToStorageModel(record),
            options,
            cancellationToken).ConfigureAwait(false);

        return this._recordKeyDecoder(result);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> UpsertBatchAsync(IEnumerable<MemoryRecord> records, UpsertRecordOptions? options = default, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var storageRecords = records.Select(this._mapper.MapFromDataToStorageModel);

        var results = this._innnerVectorStoreRecordCollection.UpsertBatchAsync(
            storageRecords,
            options,
            cancellationToken);

        await foreach (var result in results.ConfigureAwait(false))
        {
            yield return this._recordKeyDecoder(result);
        }
    }
}
