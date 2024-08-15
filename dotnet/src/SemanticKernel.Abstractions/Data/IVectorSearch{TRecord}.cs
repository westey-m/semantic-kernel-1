// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// Interface for searching a vector store.
/// </summary>
/// <typeparam name="TRecord">The record data model to use for retrieving data from the store.</typeparam>
[Experimental("SKEXP0001")]
public interface IVectorSearch<TRecord>
    where TRecord : class
{
    //// Option 1 Simple

    /// <summary>
    /// Search the vector store for records that match the given embedding and filter.
    /// </summary>
    /// <param name="vectorQuery">The vector to search with.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The records found by the vector search, including their result scores.</returns>
    IAsyncEnumerable<VectorSearchResult<TRecord>> SearchAsync(BaseVectorSearchQuery vectorQuery, CancellationToken cancellationToken = default);

    //// Option 2 Simple

    /// <summary>
    /// Search the vector store for records that match the given embedding and filter.
    /// </summary>
    /// <param name="vector">The vector to search with.</param>
    /// <param name="options">Optional options to configure the search.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The records found by the vector search, including their result scores.</returns>
    IAsyncEnumerable<VectorSearchResult<TRecord>> SearchAsync<TVectorElement>(ReadOnlyMemory<TVectorElement> vector, VectorSearchOptions? options = default, CancellationToken cancellationToken = default);

    //// Option 2 Hybrid

    /// <summary>
    /// Search the vector store for records that match the given embedding and filter.
    /// </summary>
    /// <param name="vector">The vector to search with.</param>
    /// <param name="hybridSearchObject">The object to use for the hybrid search.</param>
    /// <param name="hybridSearchFieldName">
    /// Gets or sets the name of the field to use for hybrid search. This field will typically be the one that contains the data that the embedding is generated from and the
    /// vector search will be done on the field containing the embedding. To set the vector field name, use the <see cref="VectorSearchOptions.VectorFieldName"/> property.
    /// </param>
    /// <param name="options">Optional options to configure the search.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The records found by the vector search, including their result scores.</returns>
    IAsyncEnumerable<VectorSearchResult<TRecord>> HybridSearchAsync<TVectorElement, THybridType>(ReadOnlyMemory<TVectorElement> vector, THybridType hybridSearchObject, string hybridSearchFieldName, HybridVectorSearchOptions? options = default, CancellationToken cancellationToken = default);
}
