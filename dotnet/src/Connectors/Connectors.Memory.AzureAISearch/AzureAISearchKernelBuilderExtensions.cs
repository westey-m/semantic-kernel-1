// Copyright (c) Microsoft. All rights reserved.

using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;

namespace Microsoft.SemanticKernel.Connectors.AzureAISearch;

/// <summary>
/// Extension methods to register Azure AI Search vector store instances on the <see cref="IKernelBuilder"/>.
/// </summary>
public static class AzureAISearchKernelBuilderExtensions
{
    /// <summary>Record definition for the backward compatible version of the Azure AI Search vector store.</summary>
    private static readonly VectorStoreRecordDefinition s_azureAISearchMemoryRecordDefinition = new()
    {
        Properties =
            [
                new VectorStoreRecordKeyProperty("Id"),
                new VectorStoreRecordDataProperty("Text"),
                new VectorStoreRecordDataProperty("Description"),
                new VectorStoreRecordDataProperty("AdditionalMetadata"),
                new VectorStoreRecordDataProperty("ExternalSourceName"),
                new VectorStoreRecordDataProperty("IsReference"),
                new VectorStoreRecordVectorProperty("Embedding")
            ]
    };

    /// <summary>
    /// Register an Azure AI Search vector store with the specified service ID.
    /// </summary>
    /// <param name="builder">The builder to register the vector store on.</param>
    /// <param name="serviceId">An optional service id to use as the service key.</param>
    /// <param name="generateEmbeddings">A value indicating whether to automatically generate embeddings for any data fields marked as having embeddings.</param>
    /// <param name="options">Optoinal options to further configure the vector store.</param>
    /// <returns>The kernel builder.</returns>
    public static IKernelBuilder AddAzureAISearchVectorStore(this IKernelBuilder builder, string? serviceId, bool? generateEmbeddings = default, AzureAISearchVectorStoreOptions? options = default)
    {
        builder.Services.AddKeyedTransient<IVectorStore>(
            serviceId,
            (sp, obj) =>
            {
                var vectorStore = new AzureAISearchVectorStore(
                    sp.GetRequiredService<SearchIndexClient>(),
                    options ?? sp.GetService<AzureAISearchVectorStoreOptions>());

                if (generateEmbeddings is true)
                {
                    return new TextEmbeddingVectorStore(vectorStore, sp.GetRequiredService<ITextEmbeddingGenerationService>());
                }

                return vectorStore;
            });

        return builder;
    }

    /// <summary>
    /// Register an Azure AI Search vector store record collection with the specified service ID.
    /// </summary>
    /// <param name="builder">The builder to register the vector store record collection on.</param>
    /// <param name="collectionName">The collection that the vector store record collection should access.</param>
    /// <param name="serviceId">An optional service id to use as the service key.</param>
    /// <returns>The kernel builder.</returns>
    public static IKernelBuilder AddAzureAISearchMemoryVectorStoreRecordCollection(this IKernelBuilder builder, string collectionName, string? serviceId = default)
    {
        builder.Services.AddKeyedTransient<IVectorStoreRecordCollection<string, MemoryRecord>>(
            serviceId,
            (sp, obj) =>
            {
                var vectorRecordStore = new AzureAISearchVectorStoreRecordCollection<AzureAISearchMemoryRecord>(
                    sp.GetRequiredService<SearchIndexClient>(),
                    collectionName,
                    new() { VectorStoreRecordDefinition = s_azureAISearchMemoryRecordDefinition });

                return new MemoryVectorStoreRecordCollection<string, AzureAISearchMemoryRecord>(
                    vectorRecordStore,
                    AzureAISearchMemoryRecord.EncodeId,
                    AzureAISearchMemoryRecord.DecodeId,
                    new AzureAISearchMemoryRecordMapper());
            });

        return builder;
    }
}
