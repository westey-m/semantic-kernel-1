// Copyright (c) Microsoft. All rights reserved.

using Azure.Core;
using System;
using Microsoft.SemanticKernel.Data;
using Azure;

namespace Microsoft.SemanticKernel.Connectors.AzureAISearch;

/// <summary>
/// Extension methods to register Azure AI Search <see cref="IVectorStore"/> instances on the <see cref="IKernelBuilder"/>.
/// </summary>
public static class AzureAISearchKernelBuilderExtensions
{
    /// <summary>
    /// Register an Azure AI Search <see cref="IVectorStore"/> with the specified service ID.
    /// </summary>
    /// <param name="builder">The builder to register the <see cref="IVectorStore"/> on.</param>
    /// <param name="endpoint">The service endpoint for Azure AI Search.</param>
    /// <param name="tokenCredential">The credential to authenticate to Azure AI Search with.</param>
    /// <param name="serviceId">An optional service id to use as the service key.</param>
    /// <param name="options">Optoinal options to further configure the <see cref="IVectorStore"/>.</param>
    /// <returns>The kernel builder.</returns>
    public static IKernelBuilder AddAzureAISearchVectorStore(this IKernelBuilder builder, Uri? endpoint = default, TokenCredential? tokenCredential = default, string? serviceId = default, AzureAISearchVectorStoreOptions? options = default)
    {
        builder.Services.AddAzureAISearchVectorStore(endpoint, tokenCredential, serviceId, options);
        return builder;
    }

    /// <summary>
    /// Register an Azure AI Search <see cref="IVectorStore"/> with the specified service ID.
    /// </summary>
    /// <param name="builder">The builder to register the <see cref="IVectorStore"/> on.</param>
    /// <param name="endpoint">The service endpoint for Azure AI Search.</param>
    /// <param name="credential">The credential to authenticate to Azure AI Search with.</param>
    /// <param name="serviceId">An optional service id to use as the service key.</param>
    /// <param name="options">Optoinal options to further configure the <see cref="IVectorStore"/>.</param>
    /// <returns>The kernel builder.</returns>
    public static IKernelBuilder AddAzureAISearchVectorStore(this IKernelBuilder builder, Uri? endpoint = default, AzureKeyCredential? credential = default, string? serviceId = default, AzureAISearchVectorStoreOptions? options = default)
    {
        builder.Services.AddAzureAISearchVectorStore(endpoint, credential, serviceId, options);
        return builder;
    }
}
