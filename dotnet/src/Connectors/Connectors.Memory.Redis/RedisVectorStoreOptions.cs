// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel.Connectors.Redis;

/// <summary>
/// Options when creating a <see cref="RedisVectorStore"/>.
/// </summary>
public class RedisVectorStoreOptions
{
    /// <summary>
    /// An optional factory to use for constructing <see cref="RedisVectorRecordStore{TRecord}"/> instances, if custom options are required.
    /// </summary>
    public IRedisVectorStoreCollectionFactory? VectorStoreCollectionFactory { get; init; }

    /// <summary>
    /// Indicates the way in which data should be stored in redis. Default is <see cref="RedisStorageType.Json"/>.
    /// </summary>
    public RedisStorageType? StorageType { get; init; } = RedisStorageType.Json;
}
