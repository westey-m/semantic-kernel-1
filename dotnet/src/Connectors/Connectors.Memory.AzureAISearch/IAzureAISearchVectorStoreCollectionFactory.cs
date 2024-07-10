// Copyright (c) Microsoft. All rights reserved.

using Azure.Search.Documents.Indexes;
using Microsoft.SemanticKernel.Data;

namespace Microsoft.SemanticKernel.Connectors.AzureAISearch;

/// <summary>
/// Interface for constructing <see cref="IVectorRecordStore{TKey, TRecord}"/> Azure AI Search instances when using <see cref="IVectorStore"/> to retrieve these.
/// </summary>
public interface IAzureAISearchVectorStoreCollectionFactory
{
    /// <summary>
    /// Constructs a new instance of the <see cref="IVectorRecordStore{TKey, TRecord}"/>.
    /// </summary>
    /// <typeparam name="TKey">The data type of the record key.</typeparam>
    /// <typeparam name="TRecord">The data model to use for adding, updating and retrieving data from storage.</typeparam>
    /// <param name="searchIndexClient">Azure AI Search client that can be used to manage the list of indices in an Azure AI Search Service.</param>
    /// <param name="name">The name of the collection to connect to.</param>
    /// <param name="vectorStoreRecordDefinition">An optional record definition that defines the schema of the record type. If not present, attributes on <typeparamref name="TRecord"/> will be used.</param>
    /// <returns>The new instance of <see cref="IVectorRecordStore{TKey, TRecord}"/>.</returns>
    IVectorRecordStore<TKey, TRecord> CreateVectorStoreCollection<TKey, TRecord>(SearchIndexClient searchIndexClient, string name, VectorStoreRecordDefinition? vectorStoreRecordDefinition) where TRecord : class;
}
