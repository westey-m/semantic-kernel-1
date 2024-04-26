// Copyright (c) Microsoft. All rights reserved.

using System;

namespace Microsoft.SemanticKernel.Connectors.AzureAISearch;

/// <summary>
/// Options when creating a <see cref="AzureAISearchVectorStore{TDataModel}"/>.
/// </summary>
public class AzureAISearchVectorStoreOptions
{
    /// <summary>
    /// Gets a function that can be used to encode the azure ai search record id before it is sent to Azure AI Search.
    /// </summary>
    /// <remarks>
    /// Azure AI Search keys can contain only letters, digits, underscore, dash, equal sign, recommending
    /// to encode values with a URL-safe algorithm or use base64 encoding.
    /// </remarks>
    public Func<string, string>? RecordKeyEncoder { get; init; }

    /// <summary>
    /// Gets a function that can be used to decode the azure ai search record id after it is retrieved from Azure AI Search.
    /// </summary>
    /// <remarks>
    /// Azure AI Search keys can contain only letters, digits, underscore, dash, equal sign, recommending
    /// to encode values with a URL-safe algorithm or use base64 encoding.
    /// </remarks>
    public Func<string, string>? RecordKeyDecoder { get; init; }

    /// <summary>
    /// Gets the maximum number of items to retrieve in paraellel when getting records in a batch.
    /// </summary>
    /// <remarks>Defaults to 50.</remarks>
    public int? MaxDegreeOfGetParallelism { get; init; }
}
