// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;

namespace Microsoft.SemanticKernel.Memory;

/// <summary>
/// A default record model for storing vectors and their associated data and metadata in a data store.
/// </summary>
public class VectorDBRecord
{
    /// <summary>
    /// Gets or sets the unique key associated with the vector record.
    /// </summary>
    public object Key { get; init; }

    /// <summary>
    /// Gets or sets the list of named vectors associated with the vector record.
    /// If only one vector is supported, the key should be an empty string.
    /// </summary>
    public Dictionary<string, ReadOnlyMemory<object>> NamedVectors { get; init; }

    /// <summary>
    /// Gets or sets the metadata associated with the vector record.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; }

    /// <summary>
    /// Gets or sets the data that the vectors are associated with.
    /// </summary>
    public Dictionary<string, string> Data { get; init; }
}
