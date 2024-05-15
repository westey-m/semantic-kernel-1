// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel.Memory;

/// <summary>
/// Optional options when calling <see cref="IVectorDBRecordService{TDataModel}.GetAsync"/>.
/// </summary>
public class GetRecordOptions
{
    /// <summary>
    /// Get or sets an optional collection name to use for this operation that is different to the default.
    /// </summary>
    public string? CollectionName { get; init; }

    /// <summary>
    /// Get or sets a value indicating whether to include vectors in the retrieval result.
    /// </summary>
    public bool IncludeVectors { get; init; } = false;
}
