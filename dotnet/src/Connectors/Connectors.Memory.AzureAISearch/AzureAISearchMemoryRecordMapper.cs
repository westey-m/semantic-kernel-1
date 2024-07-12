// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Memory;

namespace Microsoft.SemanticKernel.Connectors.AzureAISearch;

/// <summary>
/// Mapper to use for backward compatibility with <see cref="AzureAISearchMemoryStore"/>.
/// </summary>
internal sealed class AzureAISearchMemoryRecordMapper : IVectorStoreRecordMapper<MemoryRecord, AzureAISearchMemoryRecord>
{
    /// <inheritdoc />
    public AzureAISearchMemoryRecord MapFromDataToStorageModel(MemoryRecord dataModel)
    {
        return AzureAISearchMemoryRecord.FromMemoryRecord(dataModel);
    }

    /// <inheritdoc />
    public MemoryRecord MapFromStorageToDataModel(AzureAISearchMemoryRecord storageModel, StorageToDataModelMapperOptions options)
    {
        return storageModel.ToMemoryRecord(options.IncludeVectors);
    }
}
