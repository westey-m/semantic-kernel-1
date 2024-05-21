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
    /// Map from the consumer data model to the storage model.
    /// </summary>
    /// <param name="dataModel">The consumer data model record to map.</param>
    /// <returns>The mapped result.</returns>
    PointStruct MapFromDataToStorageModel(TDataModel dataModel);

    /// <summary>
    /// Map from the storage model to the consumer data model.
    /// </summary>
    /// <param name="storageModel">The storage data model record to map.</param>
    /// <param name="options">The <see cref="GetRecordOptions"/> of the operation that this mapping is needed for.</param>
    /// <returns>The mapped result.</returns>
    TDataModel MapFromStorageToDataModel(RetrievedPoint storageModel, GetRecordOptions? options = default);
}
