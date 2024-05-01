// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel.Memory;

/// <summary>
/// Optional options when calling <see cref="IVectorStore{TDataModel}.GetAsync"/>.
/// </summary>
public class VectorStoreGetDocumentOptions
{
    /// <summary>
    /// Get or sets a value indicating whether to include embeddings in the retrieval result.
    /// </summary>
    public bool IncludeEmbeddings { get; init; } = false;
}
