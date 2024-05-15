// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel.Connectors.Redis;

/// <summary>
/// Options when creating a <see cref="RedisVectorDBRecordService{TDataModel}"/>.
/// </summary>
public class RedisVectorDBRecordServiceOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the collection name should be prefixed to the
    /// key names before reading or writing to the redis store. Default is false.
    /// </summary>
    public bool PrefixCollectionNameToKeyNames { get; init; } = false;
}
