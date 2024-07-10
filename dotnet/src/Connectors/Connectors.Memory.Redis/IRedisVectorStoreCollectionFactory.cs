// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.Data;
using StackExchange.Redis;

namespace Microsoft.SemanticKernel.Connectors.Redis;

/// <summary>
/// Interface for constructing <see cref="IVectorRecordStore{TKey, TRecord}"/> Redis instances when using <see cref="IVectorStore"/> to retrieve these.
/// </summary>
public interface IRedisVectorStoreCollectionFactory
{
    /// <summary>
    /// Constructs a new instance of the <see cref="IVectorRecordStore{TKey, TRecord}"/>.
    /// </summary>
    /// <typeparam name="TKey">The data type of the record key.</typeparam>
    /// <typeparam name="TRecord">The data model to use for adding, updating and retrieving data from storage.</typeparam>
    /// <param name="database">The Redis database to read/write records from.</param>
    /// <param name="name">The name of the collection to connect to.</param>
    /// <param name="vectorStoreRecordDefinition">An optional record definition that defines the schema of the record type. If not present, attributes on <typeparamref name="TRecord"/> will be used.</param>
    /// <returns>The new instance of <see cref="IVectorRecordStore{TKey, TRecord}"/>.</returns>
    IVectorRecordStore<TKey, TRecord> CreateVectorStoreCollection<TKey, TRecord>(IDatabase database, string name, VectorStoreRecordDefinition? vectorStoreRecordDefinition) where TRecord : class;
}
