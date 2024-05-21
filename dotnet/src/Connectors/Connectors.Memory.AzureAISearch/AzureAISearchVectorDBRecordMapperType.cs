// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Nodes;

namespace Microsoft.SemanticKernel.Connectors.AzureAISearch;

/// <summary>
/// The types of mapper supported by <see cref="AzureAISearchVectorDBRecordService{TDataModel}"/>.
/// </summary>
public enum AzureAISearchVectorDBRecordMapperType
{
    /// <summary>
    /// Use the default mapper that is provided by the azure ai search client sdk.
    /// </summary>
    Default,

    /// <summary>
    /// Use a custom mapper between <see cref="JsonObject"/> and the data model.
    /// </summary>
    JsonObjectCustomerMapper
}
