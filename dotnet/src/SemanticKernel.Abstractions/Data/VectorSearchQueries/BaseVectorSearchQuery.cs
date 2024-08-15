// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// Contains query information to use when searching a vector store.
/// </summary>
[Experimental("SKEXP0001")]
public class BaseVectorSearchQuery
{
    internal BaseVectorSearchQuery(object? vector, object? queryData, object? searchOptions)
    {
        if (vector == null && queryData == null)
        {
            throw new ArgumentException($"At least one of the ${nameof(vector)} or ${nameof(queryData)} must be provided.");
        }

        this.Vector = vector;
        this.QueryData = queryData;
        this.SearchOptions = searchOptions;
    }

    /// <summary>
    /// Gets the vector to use when searching the vector store.
    /// </summary>
    /// <remarks>
    /// May be null if the vector is generated on the server or downstream in the pipeline.
    /// </remarks>
    public object? Vector { get; }

    /// <summary>
    /// Gets the data that needs to be searched for.
    /// </summary>
    /// <remarks>
    /// Must be provided for hybrid search scenarios.
    /// May be null if a vector is provided instead.
    /// </remarks>
    public object? QueryData { get; }

    /// <summary>
    /// Gets the options to use when searching the vector store.
    /// </summary>
    public object? SearchOptions { get; }

    // Vector search.
    public static VectorSearchQuery<TVectorElement> CreateQuery<TVectorElement>(ReadOnlyMemory<TVectorElement> vector, VectorSearchOptions? options = default) => new(vector, null, options);

    // Typed text search.
    public static VectorSearchQuery<TVectorElement, string> CreateQuery<TVectorElement>(string text, VectorSearchOptions? options = default) => new(null, text, options);

    // Untyped text search.
    // We probably don't need to support this until we have a good server generated vector use case.
    public static VectorSearchQuery CreateQuery(string text, VectorSearchOptions? options = default) => new(null, text, options);

    // Hybrid vector text search.
    public static HybridVectorSearchQuery<TVectorElement, string> CreateHybridQuery<TVectorElement>(ReadOnlyMemory<TVectorElement> vector, string text, HybridVectorSearchOptions? options = default) => new(vector, text, options);

    // Hybrid typed text search.
    public static HybridVectorSearchQuery<TVectorElement, string> CreateHybridQuery<TVectorElement>(string text, HybridVectorSearchOptions? options = default) => new(null, text, options);

    // Hybrid untyped text search.
    // We probably don't need to support this until we have a good server generated vector use case.
    public static HybridVectorSearchQuery CreateHybridQuery(string text, HybridVectorSearchOptions? options = default) => new(null, text, options);
}
