// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel.Memory.CollectionConfiguration;

/// <summary>
/// Contains configuration for creating a vector field in a data store when creating a collection.
/// </summary>
public class VectorFieldConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VectorFieldConfiguration"/> class.
    /// </summary>
    /// <param name="name">The name of the field.</param>
    /// <param name="vectorValueType">The data type that the vector will store.</param>
    /// <param name="vectorDimensions">The number of dimensions in the vector.</param>
    /// <param name="indexAlgorithmConfiguration">The configuration for the index algorithm to use when indexing the vector field.</param>
    public VectorFieldConfiguration(string name, VectorValueType vectorValueType, int vectorDimensions, IndexAlgorithmConfiguration indexAlgorithmConfiguration)
    {
        this.Name = name;
        this.VectorValueType = vectorValueType;
        this.VectorDimensions = vectorDimensions;
        this.IndexAlgorithmConfiguration = indexAlgorithmConfiguration;
    }

    /// <summary>
    /// Gets the name of the field.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Gets the data type that the vector will store.
    /// </summary>
    public VectorValueType VectorValueType { get; private set; }

    /// <summary>
    /// Gets the number of dimensions in the vector.
    /// </summary>
    public int VectorDimensions { get; private set; }

    /// <summary>
    /// Gets the configuration for the index algorithm to use when indexing the vector field.
    /// </summary>
    public IndexAlgorithmConfiguration IndexAlgorithmConfiguration { get; private set; }
}
