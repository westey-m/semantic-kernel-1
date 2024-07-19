// Copyright (c) Microsoft. All rights reserved.

using Azure.Core;
using System;
using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Data;
using Azure;

namespace Microsoft.SemanticKernel.Connectors.AzureAISearch;

/// <summary>
/// Extension methods to register Azure AI Search <see cref="IVectorStore"/> instances on an <see cref="IServiceCollection"/>.
/// </summary>
public static class AzureAISearchServiceCollectionExtensions
{
    /// <summary>
    /// Register an Azure AI Search <see cref="IVectorStore"/> with the specified service ID.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register the <see cref="IVectorStore"/> on.</param>
    /// <param name="endpoint">The service endpoint for Azure AI Search.</param>
    /// <param name="tokenCredential">The credential to authenticate to Azure AI Search with.</param>
    /// <param name="serviceId">An optional service id to use as the service key.</param>
    /// <param name="options">Optoinal options to further configure the <see cref="IVectorStore"/>.</param>
    /// <returns>The kernel builder.</returns>
    public static IServiceCollection AddAzureAISearchVectorStore(this IServiceCollection services, Uri? endpoint = default, TokenCredential? tokenCredential = default, string? serviceId = default, AzureAISearchVectorStoreOptions? options = default)
    {
        services.AddKeyedTransient<IVectorStore>(
            serviceId,
            (sp, obj) =>
            {
                var searchIndexClient = endpoint == null ? sp.GetRequiredService<SearchIndexClient>() : new SearchIndexClient(endpoint, tokenCredential);
                var selectedOptions = options ?? sp.GetService<AzureAISearchVectorStoreOptions>();

                return new AzureAISearchVectorStore(
                    searchIndexClient,
                    selectedOptions);
            });

        return services;
    }

    /// <summary>
    /// Register an Azure AI Search <see cref="IVectorStore"/> with the specified service ID.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register the <see cref="IVectorStore"/> on.</param>
    /// <param name="endpoint">The service endpoint for Azure AI Search.</param>
    /// <param name="credential">The credential to authenticate to Azure AI Search with.</param>
    /// <param name="serviceId">An optional service id to use as the service key.</param>
    /// <param name="options">Optoinal options to further configure the <see cref="IVectorStore"/>.</param>
    /// <returns>The kernel builder.</returns>
    public static IServiceCollection AddAzureAISearchVectorStore(this IServiceCollection services, Uri? endpoint = default, AzureKeyCredential? credential = default, string? serviceId = default, AzureAISearchVectorStoreOptions? options = default)
    {
        services.AddKeyedTransient<IVectorStore>(
            serviceId,
            (sp, obj) =>
            {
                var searchIndexClient = endpoint == null ? sp.GetRequiredService<SearchIndexClient>() : new SearchIndexClient(endpoint, credential);
                var selectedOptions = options ?? sp.GetService<AzureAISearchVectorStoreOptions>();

                return new AzureAISearchVectorStore(
                    searchIndexClient,
                    selectedOptions);
            });

        return services;
    }
}
