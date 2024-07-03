// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// Interface used to create new collections in a vector store using a provided configuration.
/// </summary>
[Experimental("SKEXP0001")]
public interface IConfiguredVectorCollectionCreate
{
    /// <summary>
    /// Creates a new collection with the given name and configuration in the vector store.
    /// </summary>
    /// <param name="name">The name of the new collection.</param>
    /// <param name="vectorStoreRecordDefinition">Defines the schema of the record type and is used to create the collection with.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task that completes when the collection has been created.</returns>
    Task CreateCollectionAsync(string name, VectorStoreRecordDefinition vectorStoreRecordDefinition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new collection with the given name in the vector store, by inferring the schema from the provided type and its attributes.
    /// </summary>
    /// <typeparam name="TRecord">The data type to create a collection for.</typeparam>
    /// <param name="name">The name of the new collection.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task that completes when the collection has been created.</returns>
    Task CreateCollectionAsync<TRecord>(string name, CancellationToken cancellationToken = default);
}
