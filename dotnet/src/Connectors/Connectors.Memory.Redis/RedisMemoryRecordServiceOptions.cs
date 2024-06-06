// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Nodes;
using Microsoft.SemanticKernel.Memory;

namespace Microsoft.SemanticKernel.Connectors.Redis;

/// <summary>
/// Options when creating a <see cref="RedisMemoryRecordService{TDataModel}"/>.
/// </summary>
public sealed class RedisMemoryRecordServiceOptions<TDataModel>
    where TDataModel : class
{
    /// <summary>
    /// Gets or sets a value indicating whether the collection name should be prefixed to the
    /// key names before reading or writing to the redis store. Default is false.
    /// </summary>
    public bool PrefixCollectionNameToKeyNames { get; init; } = false;

    /// <summary>
    /// Gets or sets the choice of mapper to use when converting between the data model and the redis record.
    /// </summary>
    public RedisMemoryRecordMapperType MapperType { get; init; } = RedisMemoryRecordMapperType.Default;

    /// <summary>
    /// Gets or sets an optional custom mapper to use when converting between the data model and the redis record.
    /// </summary>
    /// <remarks>
    /// Set <see cref="MapperType"/> to <see cref="RedisMemoryRecordMapperType.JsonNodeCustomMapper"/> to use this mapper."/>
    /// </remarks>
    public IMemoryRecordMapper<TDataModel, (string Key, JsonNode Node)>? JsonNodeCustomMapper { get; init; } = null;
}
