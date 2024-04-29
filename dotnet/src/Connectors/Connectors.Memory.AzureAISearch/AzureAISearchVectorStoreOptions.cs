// Copyright (c) Microsoft. All rights reserved.

using System;

namespace Microsoft.SemanticKernel.Connectors.AzureAISearch;

/// <summary>
/// Options when creating a <see cref="AzureAISearchVectorStore{TDataModel}"/>.
/// </summary>
public class AzureAISearchVectorStoreOptions
{
    /// <summary>
    /// Gets the maximum number of items to retrieve in paraellel when getting records in a batch.
    /// </summary>
    /// <remarks>Defaults to 50.</remarks>
    public int? MaxDegreeOfGetParallelism { get; init; }
}
