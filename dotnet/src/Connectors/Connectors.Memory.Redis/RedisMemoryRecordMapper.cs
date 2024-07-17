// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Text.Json;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Memory;

namespace Microsoft.SemanticKernel.Connectors.Redis;

/// <summary>
/// Mapper to use for backward compatibility with <see cref="RedisMemoryStore"/>.
/// Maps between the public <see cref="MemoryRecord"/> and the internal <see cref="RedisMemoryRecord"/> types.
/// </summary>
internal sealed class RedisMemoryRecordMapper : IVectorStoreRecordMapper<MemoryRecord, RedisMemoryRecord>
{
    /// <inheritdoc />
    public RedisMemoryRecord MapFromDataToStorageModel(MemoryRecord dataModel)
    {
        return new RedisMemoryRecord
        {
            Key = dataModel.Key,
            Metadata = JsonSerializer.Serialize(dataModel.Metadata),
            Embedding = dataModel.Embedding.ToArray(),
            Timestamp = dataModel.Timestamp.HasValue ? dataModel.Timestamp.Value.ToUnixTimeMilliseconds() : -1
        };
    }

    /// <inheritdoc />
    public MemoryRecord MapFromStorageToDataModel(RedisMemoryRecord storageModel, StorageToDataModelMapperOptions options)
    {
        return MemoryRecord.FromJsonMetadata(
            storageModel.Metadata,
            storageModel.Embedding,
            storageModel.Key,
            storageModel.Timestamp == -1 ? null : DateTimeOffset.FromUnixTimeMilliseconds(storageModel.Timestamp));
    }
}
