// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel.Memory;

/// <summary>
/// Optional options when calling <see cref="IVectorDBRecordService{TKey, TDataModel}.UpsertAsync"/>.
/// </summary>
public class UpsertRecordOptions
{
    /// <summary>
    /// Get or sets an optional collection name to use for this operation that is different to the default.
    /// </summary>
    public string? CollectionName { get; init; }
}
