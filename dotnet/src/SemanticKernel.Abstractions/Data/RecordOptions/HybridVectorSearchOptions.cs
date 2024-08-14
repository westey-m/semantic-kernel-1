// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// Options for hybrid vector search.
/// </summary>
[Experimental("SKEXP0001")]
public sealed class HybridVectorSearchOptions : VectorSearchOptions
{
    /// <summary>
    /// Gets the default options for vector search.
    /// </summary>
    public new static HybridVectorSearchOptions Default { get; } = new HybridVectorSearchOptions();

    /// <summary>
    /// Gets or sets the name of the field to search in addition to the vector field.
    /// If not provided will default to the first data field in the schema that matches the provided type of search data.
    /// </summary>
    public string? HybridFieldName { get; init; }
}
