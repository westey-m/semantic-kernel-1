// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Nodes;

namespace Microsoft.SemanticKernel.Connectors.AzureAISearch;

/// <summary>
/// Options when creating a <see cref="AzureAISearchVectorDBRecordService{TDataModel}"/>.
/// </summary>
public class AzureAISearchVectorDBRecordServiceOptions<TDataModel>
    where TDataModel : class
{
    /// <summary>
    /// Gets or sets the choice of mapper to use when converting between the data model and the azure ai search record.
    /// </summary>
    public AzureAISearchVectorDBRecordMapperType MapperType { get; init; } = AzureAISearchVectorDBRecordMapperType.Default;

    /// <summary>
    /// Gets or sets an optional custom mapper to use when converting between the data model and the azure ai search record.
    /// </summary>
    /// <remarks>
    /// Set <see cref="MapperType"/> to <see cref="AzureAISearchVectorDBRecordMapperType.JsonObjectCustomerMapper"/> to use this mapper."/>
    /// </remarks>
    public IAzureAISearchVectorDBRecordMapper<TDataModel, JsonObject>? JsonObjectCustomMapper { get; init; } = null;
}
