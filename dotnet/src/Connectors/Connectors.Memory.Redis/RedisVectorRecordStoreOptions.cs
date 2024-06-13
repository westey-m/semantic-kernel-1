// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Nodes;
using Microsoft.SemanticKernel.Memory;

namespace Microsoft.SemanticKernel.Connectors.Redis;

/// <summary>
/// Options when creating a <see cref="RedisVectorRecordStore{TRecord}"/>.
/// </summary>
public sealed class RedisVectorRecordStoreOptions<TRecord>
    where TRecord : class
{
    /// <summary>
    /// Gets or sets the default collection name to use.
    /// If not provided here, the collection name will need to be provided for each operation or the operation will throw.
    /// </summary>
    public string? DefaultCollectionName { get; init; } = null;

    /// <summary>
    /// Gets or sets a value indicating whether the collection name should be prefixed to the
    /// key names before reading or writing to the redis store. Default is false.
    /// </summary>
    public bool PrefixCollectionNameToKeyNames { get; init; } = false;

    /// <summary>
    /// Gets or sets the choice of mapper to use when converting between the data model and the redis record.
    /// </summary>
    public RedisRecordMapperType MapperType { get; init; } = RedisRecordMapperType.Default;

    /// <summary>
    /// Gets or sets an optional custom mapper to use when converting between the data model and the redis record.
    /// </summary>
    /// <remarks>
    /// Set <see cref="MapperType"/> to <see cref="RedisRecordMapperType.JsonNodeCustomMapper"/> to use this mapper."/>
    /// </remarks>
    public IVectorStoreRecordMapper<TRecord, (string Key, JsonNode Node)>? JsonNodeCustomMapper { get; init; } = null;
}
