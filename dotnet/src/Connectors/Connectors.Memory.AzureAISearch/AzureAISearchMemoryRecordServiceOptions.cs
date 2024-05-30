// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Nodes;
using Microsoft.SemanticKernel.Memory;

namespace Microsoft.SemanticKernel.Connectors.AzureAISearch;

/// <summary>
/// Options when creating a <see cref="AzureAISearchMemoryRecordService{TDataModel}"/>.
/// </summary>
public class AzureAISearchMemoryRecordServiceOptions<TDataModel>
    where TDataModel : class
{
    /// <summary>
    /// Gets or sets the choice of mapper to use when converting between the data model and the azure ai search record.
    /// </summary>
    public AzureAISearchMemoryRecordMapperType MapperType { get; init; } = AzureAISearchMemoryRecordMapperType.Default;

    /// <summary>
    /// Gets or sets an optional custom mapper to use when converting between the data model and the azure ai search record.
    /// </summary>
    /// <remarks>
    /// Set <see cref="MapperType"/> to <see cref="AzureAISearchMemoryRecordMapperType.JsonObjectCustomerMapper"/> to use this mapper."/>
    /// </remarks>
    public IMemoryRecordMapper<TDataModel, JsonObject>? JsonObjectCustomMapper { get; init; } = null;
}
