// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// Contains query information to use when doing a hybrid search in a
/// vector store using a text string, where the text string will be turned into a vector either downstream
/// in the client pipeline or on the server, if the service supports this functionality.
/// </summary>
[Experimental("SKEXP0001")]
public class HybridVectorizableTextSearchQuery : VectorSearchQuery
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HybridVectorizableTextSearchQuery"/> class.
    /// </summary>
    /// <param name="queryText">The text to use when searching the vector store. This text will be used for both the vector and keyword search portions of the search.</param>
    /// <param name="searchOptions">Options that control the behavior of the search.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="queryText"/> is not provided.</exception>
    internal HybridVectorizableTextSearchQuery(string queryText, HybridVectorSearchOptions? searchOptions)
        : base(VectorSearchQueryType.HybridVectorizableTextSearchQuery, searchOptions)
    {
        Verify.NotNullOrWhiteSpace(queryText);

        this.QueryText = queryText;
        this.SearchOptions = searchOptions;
    }

    /// <summary>
    /// Gets the text to use when searching the vector store.
    /// </summary>
    public string QueryText { get; }

    /// <summary>
    /// Gets the options to use when searching the vector store.
    /// </summary>
    public new HybridVectorSearchOptions? SearchOptions { get; }
}
