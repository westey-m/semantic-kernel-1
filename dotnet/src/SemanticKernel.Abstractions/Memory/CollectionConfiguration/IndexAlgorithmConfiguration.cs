// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel.Memory.CollectionConfiguration;

/// <summary>
/// Configuration of an index algorithm to use when indexing a vector field.
/// </summary>
public class IndexAlgorithmConfiguration
{
    /// <summary>
    /// The kind of index to use.
    /// </summary>
    public IndexKind IndexKind { get; set; }

    /// <summary>
    /// The distance function to use when comparing vectors.
    /// </summary>
    public DistanceFunction DistanceFunction { get; set; }
}
