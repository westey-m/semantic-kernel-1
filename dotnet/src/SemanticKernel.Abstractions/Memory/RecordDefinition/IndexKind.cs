﻿// Copyright (c) Microsoft. All rights reserved.

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
    Hnsw,

    /// <summary>
    /// Does a brute force search to find the nearest neighbors.
    /// Calculates the distances between all pairs of data points, so has a linear time complexity, that grows directly proportional to the number of points.
    /// Also referred to as exhaustive k nearest neighbor in some databases.
    /// </summary>
    /// <remarks>
    /// High recall accuracy, but slower and more expensive than HNSW.
    /// Better with smaller datasets.
    /// </remarks>
    Flat,
}
