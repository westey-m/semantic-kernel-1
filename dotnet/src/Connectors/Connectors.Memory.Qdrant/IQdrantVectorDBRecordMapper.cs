// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.Memory;
using Qdrant.Client.Grpc;

namespace Microsoft.SemanticKernel.Connectors.Qdrant;

/// <summary>
/// Interface for mapping between a Qdrant record and the consumer data model.
/// </summary>
/// <typeparam name="TDataModel">The consumer data model to map to or from.</typeparam>
public interface IQdrantVectorDBRecordMapper<TDataModel>
{
    /// <summary>
    /// Map from the consumer data model to a qdrant point.
    /// </summary>
    /// <param name="record">The consumer record to map.</param>
    /// <returns>The mapped result.</returns>
    PointStruct MapFromDataModelToGrpc(TDataModel record);

    /// <summary>
    /// Map from the give qdrant point to the consumer data model.
    /// </summary>
    /// <param name="point">The qdrant point to map.</param>
    /// <param name="options">The <see cref="GetRecordOptions"/> of the operation that this mapping is needed for.</param>
    /// <returns>The mapped result.</returns>
    TDataModel MapFromGrpcToDataModel(RetrievedPoint point, GetRecordOptions? options = default);
}
