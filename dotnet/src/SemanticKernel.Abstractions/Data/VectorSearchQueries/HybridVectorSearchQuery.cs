// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// Contains query information to use when doing a hybrid search on a vector store.
/// </summary>
[Experimental("SKEXP0001")]
public class HybridVectorSearchQuery : BaseVectorSearchQuery
{
    internal HybridVectorSearchQuery(object? vector, object? queryData, HybridVectorSearchOptions? searchOptions)
        : base(vector, queryData, searchOptions)
    {
        if (queryData == null)
        {
            throw new ArgumentException($"${nameof(queryData)} must be provided.");
        }

        this.SearchOptions = searchOptions;
    }

    /// <summary>
    /// Gets the options to use when searching the vector store.
    /// </summary>
    public new HybridVectorSearchOptions? SearchOptions { get; }
}
