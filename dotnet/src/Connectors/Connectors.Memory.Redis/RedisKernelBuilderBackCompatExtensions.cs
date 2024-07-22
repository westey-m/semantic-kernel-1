// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;
using StackExchange.Redis;

namespace Microsoft.SemanticKernel.Connectors.Redis;

/// <summary>
/// Extension methods to register Redis <see cref="IVectorStore"/> instances on the <see cref="IKernelBuilder"/>.
/// </summary>
public static class RedisKernelBuilderBackCompatExtensions
{
    /// <summary>
    /// Register a Redis <see cref="IVectorStore"/> with the specified service ID.
    /// </summary>
    /// <param name="builder">The builder to register the <see cref="IVectorStore"/> on.</param>
    /// <param name="serviceId">An optional service id to use as the service key.</param>
    /// <param name="generateEmbeddings">A value indicating whether to automatically generate embeddings for any data fields marked as having embeddings.</param>
    /// <param name="options">Optoinal options to further configure the <see cref="IVectorStore"/>.</param>
    /// <returns>The kernel builder.</returns>
    internal static IKernelBuilder AddRedisVectorStore(this IKernelBuilder builder, string? serviceId, bool generateEmbeddings, RedisVectorStoreOptions? options = default)
    {
        builder.Services.AddKeyedTransient<IVectorStore>(
            serviceId,
            (sp, obj) =>
            {
                var vectorStore = new RedisVectorStore(
                    sp.GetRequiredService<IDatabase>(),
                    options ?? sp.GetService<RedisVectorStoreOptions>());

                if (generateEmbeddings is true)
                {
                    return new TextEmbeddingVectorStore(vectorStore, sp.GetRequiredService<ITextEmbeddingGenerationService>());
                }

                return vectorStore;
            });

        return builder;
    }

    /// <summary>
    /// Register a Redis <see cref="IVectorStoreRecordCollection{TKey, TRecord}"/> with the specified service ID that allows backward compatible access to collections created with <see cref="MemoryRecord"/>.
    /// </summary>
    /// <param name="builder">The builder to register the <see cref="IVectorStoreRecordCollection{TKey, TRecord}"/> on.</param>
    /// <param name="collectionName">The collection that the <see cref="IVectorStoreRecordCollection{TKey, TRecord}"/> should access.</param>
    /// <param name="serviceId">An optional service id to use as the service key.</param>
    /// <returns>The kernel builder.</returns>
    public static IKernelBuilder AddRedisMemoryVectorStoreRecordCollection(this IKernelBuilder builder, string collectionName, string? serviceId = default)
    {
        builder.Services.AddKeyedTransient<IVectorStoreRecordCollection<string, MemoryRecord>>(
            serviceId,
            (sp, obj) =>
            {
                var recordCollection = new RedisHashSetVectorStoreRecordCollection<RedisMemoryRecord>(
                    sp.GetRequiredService<IDatabase>(),
                    collectionName,
                    new() { PrefixCollectionNameToKeyNames = true });

                return new MemoryVectorStoreRecordCollection<string, RedisMemoryRecord>(
                    recordCollection,
                    (key) => key,
                    (key) => key,
                    new RedisMemoryRecordMapper());
            });

        return builder;
    }
}
