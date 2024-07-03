// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// Interface for managing the collections and records in a vector store.
/// </summary>
/// <typeparam name="TKey">The data type of the record key.</typeparam>
/// <typeparam name="TRecord">The record data model to use for adding, updating and retrieving data from the store.</typeparam>
public interface IVectorStore<TKey, TRecord> : IVectorRecordStore<TKey, TRecord>, IVectorCollectionStore
    where TRecord : class
{
}
