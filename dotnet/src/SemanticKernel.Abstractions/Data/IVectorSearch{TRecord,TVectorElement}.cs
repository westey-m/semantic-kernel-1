// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// Interface for searching a vector store.
/// </summary>
/// <typeparam name="TRecord">The record data model to use for retrieving data from the store.</typeparam>
/// <typeparam name="TVectorElement">The data type of the elements in the vector.</typeparam>
[Experimental("SKEXP0001")]
public interface IVectorSearch<TRecord, TVectorElement>
    where TRecord : class
{
    //// Option 1 Simple

    /// <summary>
    /// Search the vector store for records that match the given embedding and filter.
    /// </summary>
    /// <param name="vectorQuery">The vector to search with.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The records found by the vector search, including their result scores.</returns>
    IAsyncEnumerable<VectorSearchResult<TRecord>> SearchAsync(VectorSearchQuery<TVectorElement> vectorQuery, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search the vector store for records that match the given embedding and filter.
    /// </summary>
    /// <param name="vectorQuery">The vector to search with.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The records found by the vector search, including their result scores.</returns>
    IAsyncEnumerable<VectorSearchResult<TRecord>> SearchAsync(HybridVectorSearchQuery<TVectorElement> vectorQuery, CancellationToken cancellationToken = default);
}
