// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel.Memory.CollectionConfiguration;

/// <summary>
/// Defines the index types that can be used to index vectors.
/// </summary>
public enum IndexKind
{
    /// <summary>
    /// Hierarchical Navigable Small World, which performs an aproximate nearest neighbour (ANN) search.
    /// </summary>
    HNSW
}
