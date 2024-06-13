// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.Memory;
using Qdrant.Client.Grpc;

namespace Microsoft.SemanticKernel.Connectors.Qdrant;

/// <summary>
/// Options when creating a <see cref="QdrantVectorRecordStore{TRecord}"/>.
/// </summary>
public sealed class QdrantVectorRecordStoreOptions<TRecord>
    where TRecord : class
{
    /// <summary>
    /// Gets or sets the default collection name to use.
    /// If not provided here, the collection name will need to be provided for each operation or the operation will throw.
    /// </summary>
    public string? DefaultCollectionName { get; init; } = null;

    /// <summary>
    /// Gets or sets a value indicating whether the vectors in the store are named, or whether there is just a single vector per qdrant point.
    /// Defaults to single vector per point.
    /// </summary>
    public bool HasNamedVectors { get; set; } = false;

    /// <summary>
    /// Gets or sets the choice of mapper to use when converting between the data model and the qdrant point.
    /// </summary>
    public QdrantRecordMapperType MapperType { get; init; } = QdrantRecordMapperType.Default;

    /// <summary>
    /// Gets or sets an optional custom mapper to use when converting between the data model and the qdrant point.
    /// </summary>
    /// <remarks>
    /// Set <see cref="MapperType"/> to <see cref="QdrantRecordMapperType.QdrantPointStructCustomMapper"/> to use this mapper."/>
    /// </remarks>
    public IVectorStoreRecordMapper<TRecord, PointStruct>? PointStructCustomMapper { get; init; } = null;
}
