// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel.Memory.CollectionConfiguration;

/// <summary>
/// Configuration of an index algorithm to use when indexing a vector field.
/// </summary>
public class IndexAlgorithmConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IndexAlgorithmConfiguration"/> class.
    /// </summary>
    /// <param name="indexKind">The kind of index to use.</param>
    /// <param name="distanceFunction">The distance function to use when comparing vectors.</param>
    public IndexAlgorithmConfiguration(IndexKind indexKind, DistanceFunction distanceFunction)
    {
        this.IndexKind = indexKind;
        this.DistanceFunction = distanceFunction;
    }

    /// <summary>
    /// Gets the kind of index to use.
    /// </summary>
    public IndexKind IndexKind { get; private set; }

    /// <summary>
    /// Gets the distance function to use when comparing vectors.
    /// </summary>
    public DistanceFunction DistanceFunction { get; private set; }
}
