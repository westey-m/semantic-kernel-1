// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel.Memory;

/// <summary>
/// Defines the index types that can be used to index vectors.
/// </summary>
public enum IndexKind
{
    /// <summary>
    /// Hierarchical Navigable Small World, which performs an aproximate nearest neighbour (ANN) search.
    /// </summary>
    /// <remarks>
    /// Lower accuracy than exhaustive k nearest neighbor, but faster and more efficient.
    /// </remarks>
    HNSW,

    /// <summary>
    /// Exhaustive k nearest neighbor, which calculates the distances between all pairs of data points and finds the exact k nearest neighbors for a query point.
    /// </summary>
    /// <remarks>
    /// High recall accuracy, but slower and more expensive than HNSW.
    /// Better with smaller datasets.
    /// </remarks>
    ExhaustiveKNN
}
