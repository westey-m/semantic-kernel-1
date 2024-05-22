// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.Memory;
using Qdrant.Client.Grpc;

namespace Microsoft.SemanticKernel.Connectors.Qdrant;

/// <summary>
/// Options when creating a <see cref="QdrantVectorDBRecordService{TDataModel}"/>.
/// </summary>
public class QdrantVectorDBRecordServiceOptions<TDataModel>
    where TDataModel : class
{
    /// <summary>
    /// Gets or sets a value indicating whether the vectors in the store are named, or whether there is just a single vector per qdrant point.
    /// Defaults to single vector per point.
    /// </summary>
    public bool HasNamedVectors { get; set; } = false;

    /// <summary>
    /// Gets or sets the choice of mapper to use when converting between the data model and the qdrant point.
    /// </summary>
    public QdrantVectorDBRecordMapperType MapperType { get; init; } = QdrantVectorDBRecordMapperType.Default;

    /// <summary>
    /// Gets or sets an optional custom mapper to use when converting between the data model and the qdrant point.
    /// </summary>
    /// <remarks>
    /// Set <see cref="MapperType"/> to <see cref="QdrantVectorDBRecordMapperType.QdrantPointStructCustomMapper"/> to use this mapper."/>
    /// </remarks>
    public IVectorDBRecordMapper<TDataModel, PointStruct>? PointStructCustomMapper { get; init; } = null;
}
