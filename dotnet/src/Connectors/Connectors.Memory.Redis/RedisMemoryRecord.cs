// Copyright (c) Microsoft. All rights reserved.

using System;
using Microsoft.SemanticKernel.Data;

namespace Microsoft.SemanticKernel.Connectors.Redis;

/// <summary>
/// Record type used for backward compatibility with <see cref="RedisMemoryStore"/>.
/// This can be used with a <see cref="RedisVectorStoreRecordCollection{TRecord}"/> to read data that was written to a collection using a <see cref="RedisMemoryStore"/>.
/// </summary>
internal class RedisMemoryRecord
{
    /// <summary>
    /// The key of the record.
    /// </summary>
    [VectorStoreRecordKey(StoragePropertyName = "key")]
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// A JSON-serialized string containing the metadata of the record.
    /// </summary>
    [VectorStoreRecordData(StoragePropertyName = "metadata")]
    public string Metadata { get; init; } = string.Empty;

    /// <summary>
    /// The vector embedding of the record.
    /// </summary>
    [VectorStoreRecordVector(StoragePropertyName = "embedding")]
    public ReadOnlyMemory<float> Embedding { get; init; }

    /// <summary>
    /// The timestamp of the record.
    /// </summary>
    [VectorStoreRecordData(StoragePropertyName = "timestamp")]
    public long Timestamp { get; init; }
}
