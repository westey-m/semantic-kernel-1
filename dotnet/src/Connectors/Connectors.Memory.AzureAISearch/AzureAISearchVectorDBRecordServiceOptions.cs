// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel.Connectors.AzureAISearch;

/// <summary>
/// Options when creating a <see cref="AzureAISearchVectorDBRecordService{TDataModel}"/>.
/// </summary>
public class AzureAISearchVectorDBRecordServiceOptions
{
    /// <summary>
    /// Gets the maximum number of items to retrieve in paraellel when getting records in a batch.
    /// </summary>
    /// <remarks>Defaults to 50.</remarks>
    public int? MaxDegreeOfGetParallelism { get; init; }
}
