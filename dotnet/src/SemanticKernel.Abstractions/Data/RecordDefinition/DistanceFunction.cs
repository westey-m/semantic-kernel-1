// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// Defines the distance functions that can be used to compare vectors.
/// </summary>
public enum DistanceFunction
{
    /// <summary>
    /// The cosine (angular) similarty between two vectors.
    /// </summary>
    /// <remarks>
    /// Measures only the angle between the two vectors, without taking into account the length of the vectors.
    /// ConsineSimilarity = 1 - CosineDistance.
    /// -1 means vectors are opposite.
    /// 0 means vectors are orthogonal.
    /// 1 means vectors are identical.
    /// </remarks>
    CosineSimilarity,

    /// <summary>
    /// The cosine (angular) similarty between two vectors.
    /// </summary>
    /// <remarks>
    /// CosineDistance = 1 - CosineSimilarity.
    /// 2 means vectors are opposite.
    /// 1 means vectors are orthogonal.
    /// 0 means vectors are identical.
    /// </remarks>
    CosineDistance,

    /// <summary>
    /// Measures both the length and angle between two vectors.
    /// </summary>
    /// <remarks>
    /// Same as cosine similarity if the vectors are the same length, but more performant.
    /// </remarks>
    DotProductSimilarity,

    /// <summary>
    /// Measures the Euclidean distance between two vectors.
    /// </summary>
    /// <remarks>
    /// Also known as l2-norm.
    /// </remarks>
    EuclideanDistance,

    /// <summary>
    /// Measures the Manhattan distance between two vectors.
    /// </summary>
    ManhattanDistance,
}
