// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// Options for hybrid vector search.
/// </summary>
[Experimental("SKEXP0001")]
public sealed class HybridVectorSearchOptions
{
    /// <summary>
    /// Gets the default options for vector search.
    /// </summary>
    public static HybridVectorSearchOptions Default { get; } = new HybridVectorSearchOptions();

    /// <summary>
    /// Gets or sets a basic search filter to use before doing the vector search.
    /// </summary>
    public BasicVectorSearchFilter? BasicVectorSearchFilter { get; init; } = new BasicVectorSearchFilter();

    /// <summary>
    /// Gets or sets the name of the vector field to search.
    /// If not provided will default to the first vector field in the schema.
    /// </summary>
    public string? VectorFieldName { get; init; }

    /// <summary>
    /// Gets or sets the maximum number of results to return.
    /// </summary>
    public int Limit { get; init; } = 3;

    /// <summary>
    /// Gets or sets the number of results to skip before returning results.
    /// </summary>
    public int Offset { get; init; } = 0;

    /// <summary>
    /// Gets or sets a value indicating whether to include vectors in the retrieval result.
    /// </summary>
    public bool IncludeVectors { get; init; } = false;

    /// <summary>
    /// Gets or sets the name of the field to search in addition to the vector field.
    /// If not provided will default to the first data field in the schema that matches the provided type of search data.
    /// </summary>
    public string? HybridFieldName { get; init; }
}
