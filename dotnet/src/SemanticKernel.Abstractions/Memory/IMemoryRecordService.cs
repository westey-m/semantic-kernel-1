// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.Memory;

/// <summary>
/// An interface for adding, updating, deleting and retrieving records from a memory store.
/// </summary>
[Experimental("SKEXP0001")]
public interface IMemoryRecordService
{
    /// <summary>
    /// Gets a memory record from the data store. Does not guarantee that the collection exists.
    /// </summary>
    /// <param name="key">The unique id associated with the memory record to get.</param>
    /// <param name="options">Optional options for retrieving the record.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The memory record if found, otherwise null.</returns>
    Task<object?> GetAsync(object key, GetRecordOptions? options = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a memory record from the data store. Does not guarantee that the collection exists.
    /// </summary>
    /// <param name="key">The unique id associated with the memory record to remove.</param>
    /// <param name="options">Optional options for removing the record.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The unique identifier for the memory record.</returns>
    Task<object> DeleteAsync(object key, DeleteRecordOptions? options = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts a memory record into the data store. Does not guarantee that the collection exists.
    ///     If the record already exists, it will be updated.
    ///     If the record does not exist, it will be created.
    /// </summary>
    /// <param name="record">The vecmemorytor record to upsert.</param>
    /// <param name="options">Optional options for upserting the record.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The unique identifier for the memory record.</returns>
    Task<object> UpsertAsync(object record, UpsertRecordOptions? options = default, CancellationToken cancellationToken = default);
}
