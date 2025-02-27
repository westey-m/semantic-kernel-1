// Copyright (c) Microsoft. All rights reserved.

using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Embeddings;

namespace Microsoft.SemanticKernel.Agents.Memory;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKeyedTransientVectorDataMemoryDocumentStore(
        this IServiceCollection services,
        string serviceKey,
        int embeddingDimensions,
        string storageNamespace,
        string collectionName,
        string? vectorStoreServiceKey = default)
    {
        services.AddKeyedTransient<MemoryDocumentStore>(
            serviceKey,
            (sp, _) =>
            {
                IVectorStore vectorStore = vectorStoreServiceKey != null ?
                    sp.GetRequiredKeyedService<IVectorStore>(vectorStoreServiceKey) :
                    sp.GetRequiredService<IVectorStore>();

                return new VectorDataMemoryDocumentStore<string>(
                    vectorStore,
                    sp.GetRequiredService<ITextEmbeddingGenerationService>(),
                    collectionName,
                    storageNamespace,
                    embeddingDimensions);
            });

        return services;
    }

    public static IServiceCollection AddTransientUserPreferencesMemoryDocumentStore(
        this IServiceCollection services,
        int embeddingDimensions,
        string storageNamespace,
        string collectionName = "UserPreferences",
        string? vectorStoreServiceKey = default)
    {
        return services.AddKeyedTransientVectorDataMemoryDocumentStore(
            "UserPreferencesStore",
            embeddingDimensions,
            storageNamespace,
            collectionName,
            vectorStoreServiceKey);
    }

    public static IServiceCollection AddTransientChatHistoryMemoryDocumentStore(
        this IServiceCollection services,
        int embeddingDimensions,
        string storageNamespace,
        string collectionName = "ChatHistory",
        string? vectorStoreServiceKey = default)
    {
        return services.AddKeyedTransientVectorDataMemoryDocumentStore(
            "ChatHistoryStore",
            embeddingDimensions,
            storageNamespace,
            collectionName,
            vectorStoreServiceKey);
    }
}
