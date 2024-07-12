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
/// Decorator class for <see cref="IVectorRecordStore{TKey, TRecord}"/> that allows the use of any <see cref="IVectorRecordStore{TKey, TRecord}"/> with mapping to and from <see cref="MemoryRecord"/>.
/// </summary>
public class MemoryVectorRecordStore<TStorageKey, TStorageRecord> : IVectorRecordStore<string, MemoryRecord>
    where TStorageRecord : class
{
    /// <summary>The <see cref="IVectorRecordStore{TKey, TRecord}"/> that is decorated by this class.</summary>
    private readonly IVectorRecordStore<TStorageKey, TStorageRecord> _innnerVectorRecordStore;

    /// <summary>Function that is used to encode the record id before it is written to storage or used to retrieve a record.</summary>
    private readonly Func<string, TStorageKey> _recordKeyEncoder;

    /// <summary>Function that is used to decode the record id after it is retrieved from storage or after upserting.</summary>
    private readonly Func<TStorageKey, string> _recordKeyDecoder;

    /// <summary>A mapper to map from the <see cref="MemoryRecord"/> type to the internal storage model.</summary>
    private readonly IVectorStoreRecordMapper<MemoryRecord, TStorageRecord> _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryVectorRecordStore{TKey, TStorageRecord}"/> class.
    /// </summary>
    /// <param name="innnerVectorRecordStore">The <see cref="IVectorRecordStore{TKey, TRecord}"/> that is decorated by this class.</param>
    /// <param name="recordKeyEncoder">Function that is used to encode the record id before it is written to storage or used to retrieve a record.</param>
    /// <param name="recordKeyDecoder">Function that is used to decode the record id after it is retrieved from storage or after upserting.</param>
    /// <param name="mapper">A mapper to map from the <see cref="MemoryRecord"/> type to the internal storage model.</param>
    public MemoryVectorRecordStore(
        IVectorRecordStore<TStorageKey, TStorageRecord> innnerVectorRecordStore,
        Func<string, TStorageKey> recordKeyEncoder,
        Func<TStorageKey, string> recordKeyDecoder,
        IVectorStoreRecordMapper<MemoryRecord, TStorageRecord> mapper)
    {
        // Verify.
        Verify.NotNull(innnerVectorRecordStore);
        Verify.NotNull(recordKeyEncoder);
        Verify.NotNull(recordKeyDecoder);
        Verify.NotNull(mapper);

        // Assign.
        this._innnerVectorRecordStore = innnerVectorRecordStore;
        this._recordKeyEncoder = recordKeyEncoder;
        this._recordKeyDecoder = recordKeyDecoder;
        this._mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<MemoryRecord?> GetAsync(string key, GetRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        var result = await this._innnerVectorRecordStore.GetAsync(
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
        var results = this._innnerVectorRecordStore.GetBatchAsync(
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
        await this._innnerVectorRecordStore.DeleteAsync(
            this._recordKeyEncoder(key),
            options,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteBatchAsync(IEnumerable<string> keys, DeleteRecordOptions? options = default, CancellationToken cancellationToken = default)
    {
        await this._innnerVectorRecordStore.DeleteBatchAsync(
            keys.Select(this._recordKeyEncoder),
            options,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string> UpsertAsync(MemoryRecord record, UpsertRecordOptions? options = default, CancellationToken cancellationToken = default)
    {
        var result = await this._innnerVectorRecordStore.UpsertAsync(
            this._mapper.MapFromDataToStorageModel(record),
            options,
            cancellationToken).ConfigureAwait(false);

        return this._recordKeyDecoder(result);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> UpsertBatchAsync(IEnumerable<MemoryRecord> records, UpsertRecordOptions? options = default, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var storageRecords = records.Select(this._mapper.MapFromDataToStorageModel);

        var results = this._innnerVectorRecordStore.UpsertBatchAsync(
            storageRecords,
            options,
            cancellationToken);

        await foreach (var result in results.ConfigureAwait(false))
        {
            yield return this._recordKeyDecoder(result);
        }
    }
}
