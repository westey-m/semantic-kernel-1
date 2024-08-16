// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// Contains query information to use when doing a hybrid search in a
/// vector store using a vector and text.
/// </summary>
[Experimental("SKEXP0001")]
public class HybridTextVectorizedSearchQuery<TVector> : VectorSearchQuery
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HybridTextVectorizedSearchQuery{TVector}"/> class.
    /// </summary>
    /// <param name="vector">The vector to use when searching the vector store.</param>
    /// <param name="queryText">The text to use when searching the vector store.</param>
    /// <param name="searchOptions">Options that control the behavior of the search.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="vector"/> or <paramref name="queryText"/> is not provided.</exception>
    internal HybridTextVectorizedSearchQuery(TVector vector, string queryText, HybridVectorSearchOptions? searchOptions)
        : base(VectorSearchQueryType.HybridTextVectorizedSearchQuery, searchOptions)
    {
        Verify.NotNull(vector);
        Verify.NotNullOrWhiteSpace(queryText);

        this.Vector = vector;
        this.QueryText = queryText;
        this.SearchOptions = searchOptions;
    }

    /// <summary>
    /// Gets the vector to use when searching the vector store.
    /// </summary>
    public TVector Vector { get; }

    /// <summary>
    /// Gets the text to use when searching the vector store.
    /// </summary>
    public string QueryText { get; }

    /// <summary>
    /// Gets the options that control the behavior of the search.
    /// </summary>
    public new HybridVectorSearchOptions? SearchOptions { get; }
}
