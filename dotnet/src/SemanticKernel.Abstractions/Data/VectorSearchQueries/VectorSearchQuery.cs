// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// Contains query information to use when searching a vector store.
/// </summary>
[Experimental("SKEXP0001")]
public class VectorSearchQuery : BaseVectorSearchQuery
{
    internal VectorSearchQuery(object? vector, object? queryData, VectorSearchOptions? searchOptions)
        : base(vector, queryData, searchOptions)
    {
        this.SearchOptions = searchOptions;
    }

    /// <summary>
    /// Gets the options to use when searching the vector store.
    /// </summary>
    public new VectorSearchOptions? SearchOptions { get; }
}
