// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// Interface for searching a vector store using data that will be turned into an embedding before search.
/// </summary>
/// <typeparam name="TRecord">The record data model to use for retrieving data from the store.</typeparam>
[Experimental("SKEXP0001")]
public interface ITextEmbeddingSearch<TRecord>
    where TRecord : class
{
    /// <summary>
    /// Search the vector store for records that match the given text and filter.
    /// </summary>
    /// <param name="searchText">The string to do a vector search with.</param>
    /// <param name="options">Optional options to configure the search.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The records found by the vector search, including their result scores.</returns>
    IAsyncEnumerable<VectorSearchResult<TRecord>> SearchAsync<TVectorElement>(string searchText, VectorSearchOptions? options = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search the vector store for records that match the given text and filter.
    /// </summary>
    /// <param name="searchText">The string to do a vector search with.</param>
    /// <param name="hybridSearchText">The search text to use for the text portion of the hybrid search if different text is required to that used for the vector search.</param>
    /// <param name="hybridSearchFieldName">
    /// Gets or sets the name of the field to use for hybrid search. This field will typically be the one that contains the data that the embedding is generated from and the
    /// vector search will be done on the field containing the embedding. To set the vector field name, use the <see cref="VectorSearchOptions.VectorFieldName"/> property.
    /// </param>
    /// <param name="options">Optional options to configure the search.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The records found by the vector search, including their result scores.</returns>
    IAsyncEnumerable<VectorSearchResult<TRecord>> HybridTextSearchAsync<TVectorElement>(string searchText, string? hybridSearchText, string hybridSearchFieldName, VectorSearchOptions? options = default, CancellationToken cancellationToken = default);
}
