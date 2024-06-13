// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;

namespace Microsoft.SemanticKernel.Connectors.Qdrant;

/// <summary>
/// Options when creating a <see cref="QdrantVectorStoreRecordMapper"/>.
/// </summary>
public sealed class QdrantVectorStoreRecordMapperOptions
{
    /// <summary>Empty string list used for initialization.</summary>
    private static readonly List<string> s_emptyStringList = new();

    /// <summary>
    /// Gets or sets a value indicating whether the vectors in the store are named, or whether there is just a single vector per qdrant point.
    /// Defaults to single vector per point.
    /// </summary>
    public bool HasNamedVectors { get; set; } = false;

    /// <summary>
    /// Gets or sets the names of fields that contain the string fragments that are used to create embeddings.
    /// </summary>
    public IReadOnlyList<string> StringDataFieldNames { get; init; } = s_emptyStringList;

    /// <summary>
    /// Gets or sets the names of fields that contain additional data. This can be any data that the embedding is not based on.
    /// </summary>
    public IReadOnlyList<string> MetadataFieldNames { get; init; } = s_emptyStringList;
}
