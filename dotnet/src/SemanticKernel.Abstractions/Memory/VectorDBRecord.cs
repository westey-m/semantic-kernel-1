// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;

namespace Microsoft.SemanticKernel.Memory;

/// <summary>
/// A default record model for storing vectors and their associated data and metadata in a data store.
/// </summary>
public class VectorDBRecord
{
    /// <summary>Empty static dictionary for initialization purposes.</summary>
    private static readonly Dictionary<string, ReadOnlyMemory<object>?> s_emptyVectors = new();

    /// <summary>Empty static dictionary for initialization purposes.</summary>
    private static readonly Dictionary<string, object?> s_emptyMetadata = new();

    /// <summary>Empty static dictionary for initialization purposes.</summary>
    private static readonly Dictionary<string, string?> s_emptyStringData = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorDBRecord"/> class.
    /// </summary>
    /// <param name="key">The key that uniquely identifies the record.</param>
    public VectorDBRecord(object key)
    {
        this.Key = key;
    }

    /// <summary>
    /// Gets or sets the unique key associated with the vector record.
    /// </summary>
    public object Key { get; init; }

    /// <summary>
    /// Gets or sets the list of named vectors associated with the vector record.
    /// If only one vector is supported, the key should be an empty string.
    /// </summary>
    public IReadOnlyDictionary<string, ReadOnlyMemory<object>?> Vectors { get; init; } = s_emptyVectors;

    /// <summary>
    /// Gets or sets the metadata associated with the vector record.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Metadata { get; init; } = s_emptyMetadata;

    /// <summary>
    /// Gets or sets the string data that the vectors are associated with.
    /// </summary>
    public IReadOnlyDictionary<string, string?> StringData { get; init; } = s_emptyStringData;
}
