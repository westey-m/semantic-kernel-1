// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;

namespace Microsoft.SemanticKernel.Memory;

/// <summary>
/// An interface for adding, updating, deleting and retrieving records from an vector store.
/// </summary>
/// <typeparam name="TDataModel">The data model to use for adding, updating and retrieving data from storage.</typeparam>
[Experimental("SKEXP0001")]
public interface IVectorStore<TDataModel>
{
    /// <summary>
    /// Gets a memory record from the data store. Does not guarantee that the collection exists.
    /// </summary>
    /// <param name="collectionName">The name of the collection of records.</param>
    /// <param name="key">The unique id associated with the memory record to get.</param>
    /// <param name="options">Optional options for retrieving the record.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The memory record if found, otherwise null.</returns>
    Task<TDataModel?> GetAsync(string collectionName, string key, VectorStoreGetDocumentOptions? options = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a batch of memory records from the data store. Does not guarantee that the collection exists.
    /// </summary>
    /// <param name="collectionName">The name of the collection of records.</param>
    /// <param name="keys">The unique ids associated with the memory record to get.</param>
    /// <param name="options">Optional options for retrieving the records.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The memory records associated with the unique keys provided.</returns>
    IAsyncEnumerable<TDataModel> GetBatchAsync(string collectionName, IEnumerable<string> keys, VectorStoreGetDocumentOptions? options = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a memory record from the data store. Does not guarantee that the collection exists.
    /// </summary>
    /// <param name="collectionName">The name of the collection of records.</param>
    /// <param name="key">The unique id associated with the memory record to remove.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The unique identifier for the memory record.</returns>
    Task<string> RemoveAsync(string collectionName, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a batch of memory records from the data store. Does not guarantee that the collection exists.
    /// </summary>
    /// <param name="collectionName">The name of the collection of records.</param>
    /// <param name="keys">The unique ids associated with the memory record to remove.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    Task RemoveBatchAsync(string collectionName, IEnumerable<string> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts a memory record into the data store. Does not guarantee that the collection exists.
    ///     If the record already exists, it will be updated.
    ///     If the record does not exist, it will be created.
    /// </summary>
    /// <param name="collectionName">The name of the collection of records.</param>
    /// <param name="record">The memory record to upsert.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The unique identifier for the memory record.</returns>
    Task<string> UpsertAsync(string collectionName, TDataModel record, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts a group of memory records into the data store. Does not guarantee that the collection exists.
    ///     If the record already exists, it will be updated.
    ///     If the record does not exist, it will be created.
    /// </summary>
    /// <param name="collectionName">The name of the collection of records.</param>
    /// <param name="records">The memory records to upsert.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The unique identifiers for the memory records.</returns>
    IAsyncEnumerable<string> UpsertBatchAsync(string collectionName, IEnumerable<TDataModel> records, CancellationToken cancellationToken = default);
}
