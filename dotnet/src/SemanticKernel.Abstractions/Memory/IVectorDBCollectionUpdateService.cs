// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.Memory;

/// <summary>
/// Interface used to do non-create operations on collections in a vector database.
/// </summary>
public interface IVectorDBCollectionUpdateService
{
    /// <summary>
    /// Retrieve the names of all the collections in the vector database.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The list of names of all the collections in the vector database.</returns>
    Task<IEnumerable<string>> GetCollectionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a collection exists in the vector database.
    /// </summary>
    /// <param name="name">The name of the collection to check for.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns><see langword="true"/> if the collection exists, <see langword="false"/> otherwise.</returns>
    Task<bool> DoesCollectionExistAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a collection from the vector database.
    /// </summary>
    /// <param name="name">The name of the collection delete.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task that completes when the collection has been deleted.</returns>
    Task DeleteCollectionAsync(string name, CancellationToken cancellationToken = default);
}
