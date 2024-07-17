// Copyright (c) Microsoft. All rights reserved.

using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;

namespace Microsoft.SemanticKernel.Connectors.AzureAISearch;

/// <summary>
/// Extension methods to register Azure AI Search <see cref="IVectorStore"/> instances on the <see cref="IKernelBuilder"/>.
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
    /// Register an Azure AI Search <see cref="IVectorStore"/> with the specified service ID.
    /// </summary>
    /// <param name="builder">The builder to register the <see cref="IVectorStore"/> on.</param>
    /// <param name="serviceId">An optional service id to use as the service key.</param>
    /// <param name="generateEmbeddings">A value indicating whether to automatically generate embeddings for any data fields marked as having embeddings.</param>
    /// <param name="options">Optoinal options to further configure the <see cref="IVectorStore"/>.</param>
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
    /// Register an Azure AI Search <see cref="IVectorStoreRecordCollection{TKey, TRecord}"/> with the specified service ID that allows backward compatible access to collections created with <see cref="MemoryRecord"/>.
    /// </summary>
    /// <param name="builder">The builder to register the <see cref="IVectorStoreRecordCollection{TKey, TRecord}"/> on.</param>
    /// <param name="collectionName">The collection that the <see cref="IVectorStoreRecordCollection{TKey, TRecord}"/> should access.</param>
    /// <param name="serviceId">An optional service id to use as the service key.</param>
    /// <returns>The kernel builder.</returns>
    public static IKernelBuilder AddAzureAISearchMemoryVectorStoreRecordCollection(this IKernelBuilder builder, string collectionName, string? serviceId = default)
    {
        builder.Services.AddKeyedTransient<IVectorStoreRecordCollection<string, MemoryRecord>>(
            serviceId,
            (sp, obj) =>
            {
                var recordCollection = new AzureAISearchVectorStoreRecordCollection<AzureAISearchMemoryRecord>(
                    sp.GetRequiredService<SearchIndexClient>(),
                    collectionName,
                    new() { VectorStoreRecordDefinition = s_azureAISearchMemoryRecordDefinition });

                return new MemoryVectorStoreRecordCollection<string, AzureAISearchMemoryRecord>(
                    recordCollection,
                    AzureAISearchMemoryRecord.EncodeId,
                    AzureAISearchMemoryRecord.DecodeId,
                    new AzureAISearchMemoryRecordMapper());
            });

        return builder;
    }
}
