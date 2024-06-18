// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.Memory;

/// <summary>
/// Attribute to mark a property on a record class as the vector.
/// </summary>
[Experimental("SKEXP0001")]
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class VectorStoreRecordVectorAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VectorStoreRecordVectorAttribute"/> class.
    /// </summary>
    public VectorStoreRecordVectorAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorStoreRecordVectorAttribute"/> class.
    /// </summary>
    /// <param name="Dimensions">The number of dimensions that the vector has.</param>
    public VectorStoreRecordVectorAttribute(int Dimensions)
    {
        this.Dimensions = Dimensions;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorStoreRecordVectorAttribute"/> class.
    /// </summary>
    /// <param name="Dimensions">The number of dimensions that the vector has.</param>
    /// <param name="IndexKind">The kind of index to use.</param>
    /// <param name="DistanceFunction">The distance function to use when comparing vectors.</param>
    public VectorStoreRecordVectorAttribute(int Dimensions, IndexKind IndexKind, DistanceFunction DistanceFunction)
    {
        this.Dimensions = Dimensions;
        this.IndexKind = IndexKind;
        this.DistanceFunction = DistanceFunction;
    }

    /// <summary>
    /// Gets or sets the number of dimensions that the vector has.
    /// </summary>
    public int? Dimensions { get; private set; }

    /// <summary>
    /// Gets the kind of index to use.
    /// </summary>
    public IndexKind? IndexKind { get; private set; }

    /// <summary>
    /// Gets the distance function to use when comparing vectors.
    /// </summary>
    public DistanceFunction? DistanceFunction { get; private set; }
}
