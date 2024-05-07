// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel.Connectors.Qdrant;

/// <summary>
/// Options when creating a <see cref="QdrantVectorStore{TDataModel}"/>.
/// </summary>
public class QdrantVectorStoreOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the vectors in the store are named, or whether there is just a single vector per qdrant point.
    /// Defaults to single vector per point.
    /// </summary>
    public bool HasNamedVectors { get; set; } = false;

    /// <summary>
    /// Gets or sets the type of id that we are using with Qdrant.
    /// Defaults to UUID.
    /// </summary>
    public QdrantIdType IdType { get; set; } = QdrantIdType.UUID;

    /// <summary>
    /// Enum describing the choice of id types that we support with Qdrant.
    /// </summary>
    public enum QdrantIdType
    {
        /// <summary>
        /// The id is a UUID stored as a string.
        /// </summary>
        UUID,

        /// <summary>
        /// The id is a unsigned long stored as a ulong.
        /// </summary>
        Ulong
    }
}
