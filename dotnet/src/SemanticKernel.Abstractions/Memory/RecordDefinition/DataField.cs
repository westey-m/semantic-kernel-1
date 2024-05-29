// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.Memory;

/// <summary>
/// A description of a data field for storage in a memory store.
/// </summary>
[Experimental("SKEXP0001")]
public sealed class DataField : Field
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataField"/> class.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    public DataField(string fieldName)
        : base(fieldName)
    {
    }

    /// <summary>
    /// Gets or sets a value indicating whether this data field has an associated embedding field.
    /// </summary>
    /// <remarks>Defaults to <see langword="false" /></remarks>
    public bool HasEmbedding { get; init; }

    /// <summary>
    /// Gets or sets the name of the property that contains the embedding for this data field.
    /// </summary>
    public string? EmbeddingPropertyName { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether this data field should be / is filterable.
    /// </summary>
    /// <remarks>Defaults to <see langword="false" /></remarks>
    public bool IsFilterable { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether this data field should be / is full text searchable.
    /// </summary>
    /// <remarks>Defaults to <see langword="false" /></remarks>
    public bool IsFullTextSearchable { get; init; }
}
