// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel.Memory.CollectionConfiguration;

/// <summary>
/// Contains configuration for creating a vector field in a data store when creating a collection.
/// </summary>
public class VectorFieldConfiguration
{
    /// <summary>
    /// The name of the field.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The data type that the vector will store.
    /// </summary>
    public VectorValueType VectorValueType { get; set; }

    /// <summary>
    /// The number of dimensions in the vector.
    /// </summary>
    public int VectorDimensions { get; set; }

    /// <summary>
    /// The configuration for the index algorithm to use when indexing the vector field.
    /// </summary>
    public IndexAlgorithmConfiguration IndexAlgorithmConfiguration { get; set; }
}
