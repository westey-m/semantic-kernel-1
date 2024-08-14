// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.Data;

[Experimental("SKEXP0001")]
public class HybridVectorSearchQuery<TVectorElement, TQueryData> : VectorSearchQuery
{
    internal HybridVectorSearchQuery(ReadOnlyMemory<TVectorElement>? vector, TQueryData queryData, HybridVectorSearchOptions? searchOptions)
        : base(vector, queryData, searchOptions)
    {
        if (queryData == null)
        {
            throw new ArgumentException($"${nameof(queryData)} must be provided.");
        }

        this.Vector = vector;
        this.QueryData = queryData;
        this.SearchOptions = searchOptions;
    }

    /// <summary>
    /// The vector to use when searching the vector store.
    /// </summary>
    public new ReadOnlyMemory<TVectorElement>? Vector { get; init; }

    /// <summary>
    /// The data that needs to be searched for.
    /// </summary>
    public new TQueryData QueryData { get; init; }

    /// <summary>
    /// Gets the options to use when searching the vector store.
    /// </summary>
    public HybridVectorSearchOptions? SearchOptions { get; }
}
