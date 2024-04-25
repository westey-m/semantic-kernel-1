// Copyright (c) Microsoft. All rights reserved.

using System;

namespace Microsoft.SemanticKernel.Connectors.AzureAISearch;

/// <summary>
/// Options when creating a <see cref="AzureAISearchVectorStore{TDataModel}"/>.
/// </summary>
public class AzureAISearchVectorStoreOptions
{
    /// <summary>
    /// Gets a function that can be used to sanitize the azure ai search record id before it is sent to Azure AI Search.
    /// </summary>
    /// <remarks>
    /// Azure AI Search keys can contain only letters, digits, underscore, dash, equal sign, recommending
    /// to encode values with a URL-safe algorithm.
    /// </remarks>
    public Func<string, string>? RecordKeySanitizer { get; init; }

    /// <summary>
    /// Gets the name of the field that is the key of the azure ai search record.
    /// This is required if the key field name should be sanitized.
    /// </summary>
    public string? KeyFieldName { get; init; }

    /// <summary>
    /// Gets the maximum number of items to retrieve in paraellel when getting records in a batch.
    /// </summary>
    /// <remarks>Defaults to 50.</remarks>
    public int? MaxDegreeOfGetParallelism { get; init; }
}
