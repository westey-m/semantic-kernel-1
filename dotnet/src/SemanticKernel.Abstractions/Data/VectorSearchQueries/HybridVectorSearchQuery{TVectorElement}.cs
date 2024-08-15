// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// Contains query information to use when doing a hybrid search on a vector store.
/// </summary>
[Experimental("SKEXP0001")]
public class HybridVectorSearchQuery<TVectorElement> : HybridVectorSearchQuery
{
    internal HybridVectorSearchQuery(ReadOnlyMemory<TVectorElement>? vector, object? queryData, HybridVectorSearchOptions? searchOptions)
        : base(vector, queryData, searchOptions)
    {
        if (queryData == null)
        {
            throw new ArgumentException($"${nameof(queryData)} must be provided.");
        }

        this.Vector = vector;
    }

    /// <summary>
    /// The vector to use when searching the vector store.
    /// </summary>
    public new ReadOnlyMemory<TVectorElement>? Vector { get; init; }
}
