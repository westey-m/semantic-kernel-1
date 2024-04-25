// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;

namespace Microsoft.SemanticKernel.Memory;

/// <summary>
/// Optional options when calling <see cref="IVectorStore{TDataModel}.GetAsync"/>.
/// </summary>
public class VectorStoreGetDocumentOptions
{
    /// <summary>
    /// Gets the names of the fields to retrieve, in case only a subset of fields is needed.
    /// </summary>
    public IList<string> SelectedFields { get; init; } = new List<string>();
}
