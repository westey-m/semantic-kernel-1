// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.SemanticKernel.Data;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Microsoft.SemanticKernel.Connectors.Qdrant;

/// <summary>
/// Provides collection retrieval and deletion for Qdrant.
/// </summary>
public sealed class QdrantVectorStore : IVectorStore
{
    /// <summary>Qdrant client that can be used to manage the collections and points in a Qdrant store.</summary>
    private readonly QdrantClient _qdrantClient;

    /// <summary>Optional configuration options for this class.</summary>
    private readonly QdrantVectorStoreOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="QdrantVectorStore"/> class.
    /// </summary>
    /// <param name="qdrantClient">Qdrant client that can be used to manage the collections and points in a Qdrant store.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    public QdrantVectorStore(QdrantClient qdrantClient, QdrantVectorStoreOptions? options = default)
    {
        Verify.NotNull(qdrantClient);

        this._qdrantClient = qdrantClient;
        this._options = options ?? new QdrantVectorStoreOptions();
    }

    /// <inheritdoc />
    public IVectorStoreRecordCollection<TKey, TRecord> GetCollection<TKey, TRecord>(string name, VectorStoreRecordDefinition? vectorStoreRecordDefinition = null) where TRecord : class
    {
        if (typeof(TKey) != typeof(ulong) && typeof(TKey) != typeof(Guid))
        {
            throw new NotSupportedException("Only ulong and Guid keys are supported.");
        }

        if (this._options.VectorStoreCollectionFactory is not null)
        {
            return this._options.VectorStoreCollectionFactory.CreateVectorStoreRecordCollection<TKey, TRecord>(this._qdrantClient, name, vectorStoreRecordDefinition);
        }

        var directlyCreatedStore = new QdrantVectorStoreRecordCollection<TRecord>(this._qdrantClient, name, new QdrantVectorStoreRecordCollectionOptions<TRecord>() { VectorStoreRecordDefinition = vectorStoreRecordDefinition }) as IVectorStoreRecordCollection<TKey, TRecord>;
        return directlyCreatedStore!;
    }

    /// <inheritdoc />
    public async Task<IVectorStoreRecordCollection<TKey, TRecord>> CreateCollectionAsync<TKey, TRecord>(string name, VectorStoreRecordDefinition? vectorStoreRecordDefinition = null, CancellationToken cancellationToken = default) where TRecord : class
    {
        if (typeof(TKey) != typeof(ulong) && typeof(TKey) != typeof(Guid))
        {
            throw new NotSupportedException("Only ulong and Guid keys are supported.");
        }

        if (vectorStoreRecordDefinition is null)
        {
            vectorStoreRecordDefinition = VectorStoreRecordPropertyReader.CreateVectorStoreRecordDefinitionFromType(typeof(TRecord), true);
        }

        if (!this._options.HasNamedVectors)
        {
            // If we are not using named vectors, we can only have one vector property.
            var singleVectorProperty = vectorStoreRecordDefinition.Properties.First(x => x is VectorStoreRecordVectorProperty vectorProperty) as VectorStoreRecordVectorProperty;

            if (singleVectorProperty!.Dimensions is not > 0)
            {
                throw new InvalidOperationException($"Property {nameof(singleVectorProperty.Dimensions)} on {nameof(VectorStoreRecordVectorProperty)} '{singleVectorProperty.PropertyName}' must be set to a positive ingeteger to create a collection.");
            }

            if (singleVectorProperty!.IndexKind is not null && singleVectorProperty!.IndexKind is not IndexKind.Hnsw)
            {
                throw new InvalidOperationException($"Unsupported index kind '{singleVectorProperty!.IndexKind}' for {nameof(VectorStoreRecordVectorProperty)} '{singleVectorProperty.PropertyName}'.");
            }

            // Create the collection with the single unnamed vector.
            await this._qdrantClient.CreateCollectionAsync(
                name,
                new VectorParams { Size = (ulong)singleVectorProperty.Dimensions, Distance = QdrantVectorStoreCollectionCreateMapping.GetSDKDistanceAlgorithm(singleVectorProperty) },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var vectorParamsMap = new VectorParamsMap();

            // Since we are using named vectors, iterate over all vector properties.
            var vectorProperties = vectorStoreRecordDefinition.Properties.Where(x => x is VectorStoreRecordVectorProperty).Select(x => (VectorStoreRecordVectorProperty)x);
            foreach (var vectorProperty in vectorProperties)
            {
                if (vectorProperty.Dimensions is not > 0)
                {
                    throw new InvalidOperationException($"Property {nameof(vectorProperty.Dimensions)} on {nameof(VectorStoreRecordVectorProperty)} '{vectorProperty.PropertyName}' must be set to a positive ingeteger to create a collection.");
                }

                if (vectorProperty.IndexKind is not null && vectorProperty.IndexKind is not IndexKind.Hnsw)
                {
                    throw new InvalidOperationException($"Unsupported index kind '{vectorProperty.IndexKind}' for {nameof(VectorStoreRecordVectorProperty)} '{vectorProperty.PropertyName}'.");
                }

                // Add each vector property to the vectors map.
                vectorParamsMap.Map.Add(
                    vectorProperty.PropertyName,
                    new VectorParams
                    {
                        Size = (ulong)vectorProperty.Dimensions,
                        Distance = QdrantVectorStoreCollectionCreateMapping.GetSDKDistanceAlgorithm(vectorProperty)
                    });
            }

            // Create the collection with named vectors.
            await this._qdrantClient.CreateCollectionAsync(
                name,
                vectorParamsMap,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        // Add indexes for each of the data properties that require filtering.
        var dataProperties = vectorStoreRecordDefinition.Properties.Where(x => x is VectorStoreRecordDataProperty).Select(x => (VectorStoreRecordDataProperty)x).Where(x => x.IsFilterable);
        foreach (var dataProperty in dataProperties)
        {
            if (dataProperty.PropertyType is null)
            {
                throw new InvalidOperationException($"Property {nameof(dataProperty.PropertyType)} on {nameof(VectorStoreRecordDataProperty)} '{dataProperty.PropertyName}' must be set to create a collection, since the property is filterable.");
            }

            await this._qdrantClient.CreatePayloadIndexAsync(
                name,
                dataProperty.PropertyName,
                QdrantVectorStoreCollectionCreateMapping.s_schemaTypeMap[dataProperty.PropertyType!],
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return this.GetCollection<TKey, TRecord>(name, vectorStoreRecordDefinition);
    }

    /// <inheritdoc />
    public async Task<IVectorStoreRecordCollection<TKey, TRecord>> CreateCollectionIfNotExistsAsync<TKey, TRecord>(string name, VectorStoreRecordDefinition? vectorStoreRecordDefinition = null, CancellationToken cancellationToken = default) where TRecord : class
    {
        if (typeof(TKey) != typeof(ulong) && typeof(TKey) != typeof(Guid))
        {
            throw new NotSupportedException("Only ulong and Guid keys are supported.");
        }

        if (!await this.CollectionExistsAsync(name, cancellationToken).ConfigureAwait(false))
        {
            return await this.CreateCollectionAsync<TKey, TRecord>(name, vectorStoreRecordDefinition, cancellationToken).ConfigureAwait(false);
        }

        return this.GetCollection<TKey, TRecord>(name, vectorStoreRecordDefinition);
    }

    /// <inheritdoc />
    public async Task DeleteCollectionAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            await this._qdrantClient.DeleteCollectionAsync(name, null, cancellationToken).ConfigureAwait(false);
        }
        catch (RpcException ex)
        {
            throw new HttpOperationException(ex.Message, ex);
        }
    }

    /// <inheritdoc />
    public async Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            return await this._qdrantClient.CollectionExistsAsync(name, cancellationToken).ConfigureAwait(false);
        }
        catch (RpcException ex)
        {
            throw new HttpOperationException(ex.Message, ex);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> ListCollectionNamesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> collections;

        try
        {
            collections = await this._qdrantClient.ListCollectionsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (RpcException ex)
        {
            throw new HttpOperationException(ex.Message, ex);
        }

        foreach (var collection in collections)
        {
            yield return collection;
        }
    }
}
