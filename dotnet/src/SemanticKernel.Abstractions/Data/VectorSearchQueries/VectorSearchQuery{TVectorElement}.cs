// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// Contains query information to use when searching a vector store.
/// </summary>
/// <typeparam name="TVectorElement">The type of the vector elements.</typeparam>
[Experimental("SKEXP0001")]
public class VectorSearchQuery<TVectorElement> : VectorSearchQuery
{
    internal VectorSearchQuery(ReadOnlyMemory<TVectorElement>? vector, object? queryData, VectorSearchOptions? searchOptions)
        : base(vector, queryData, searchOptions)
    {
        this.Vector = vector!.Value;
    }

    /// <summary>
    /// The vector to use when searching the vector store.
    /// </summary>
    public new ReadOnlyMemory<TVectorElement> Vector { get; init; }
}
