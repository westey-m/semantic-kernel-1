// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// Contains a vector to use when searching a vector store.
/// </summary>
/// <typeparam name="TVectorElement">The type of the vector elements.</typeparam>
/// <typeparam name="TQueryData">The type of the data that needs to be searched for.</typeparam>
[Experimental("SKEXP0001")]
public class VectorSearchQuery<TVectorElement, TQueryData> : VectorSearchQuery<TVectorElement>
{
    internal VectorSearchQuery(ReadOnlyMemory<TVectorElement>? vector, TQueryData? queryData, VectorSearchOptions? vectorSearchOptions)
        : base(vector, queryData, vectorSearchOptions)
    {
        if (vector == null && queryData == null)
        {
            throw new ArgumentException($"At least one of the ${nameof(vector)} or ${nameof(queryData)} must be provided.");
        }

        this.Vector = vector;
        this.QueryData = queryData;
    }

    /// <summary>
    /// The vector to use when searching the vector store.
    /// </summary>
    public new ReadOnlyMemory<TVectorElement>? Vector { get; init; }

    /// <summary>
    /// The data that needs to be searched for.
    /// </summary>
    public new TQueryData? QueryData { get; init; }
}
