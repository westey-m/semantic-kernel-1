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

namespace Microsoft.SemanticKernel.Connectors.Qdrant;

/// <summary>
/// Provides collection retrieval and deletion for Qdrant.
/// </summary>
public sealed class QdrantVectorStore : IVectorStore
{
    /// <summary>The name of this database for telemetry purposes.</summary>
    private const string DatabaseName = "Qdrant";

    /// <summary>Qdrant client that can be used to manage the collections and points in a Qdrant store.</summary>
    private readonly MockableQdrantClient _qdrantClient;

    /// <summary>Optional configuration options for this class.</summary>
    private readonly QdrantVectorStoreOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="QdrantVectorStore"/> class.
    /// </summary>
    /// <param name="qdrantClient">Qdrant client that can be used to manage the collections and points in a Qdrant store.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    public QdrantVectorStore(QdrantClient qdrantClient, QdrantVectorStoreOptions? options = default)
        : this(new MockableQdrantClient(qdrantClient), options)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QdrantVectorStore"/> class.
    /// </summary>
    /// <param name="qdrantClient">Qdrant client that can be used to manage the collections and points in a Qdrant store.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    internal QdrantVectorStore(MockableQdrantClient qdrantClient, QdrantVectorStoreOptions? options = default)
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
            return this._options.VectorStoreCollectionFactory.CreateVectorStoreRecordCollection<TKey, TRecord>(this._qdrantClient.QdrantClient, name, vectorStoreRecordDefinition);
        }

        var directlyCreatedStore = new QdrantVectorStoreRecordCollection<TRecord>(this._qdrantClient, name, new QdrantVectorStoreRecordCollectionOptions<TRecord>() { VectorStoreRecordDefinition = vectorStoreRecordDefinition });
        var castCreatedStore = directlyCreatedStore as IVectorStoreRecordCollection<TKey, TRecord>;
        return castCreatedStore!;
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
            // If we are not using named vectors, we can only have one vector property. We can assume we have at least one, since this is already verified in the constructor.
            var singleVectorProperty = vectorStoreRecordDefinition.Properties.First(x => x is VectorStoreRecordVectorProperty vectorProperty) as VectorStoreRecordVectorProperty;

            // Map the single vector property to the qdrant config.
            var vectorParams = QdrantVectorStoreCollectionCreateMapping.MapSingleVector(singleVectorProperty!);

            // Create the collection with the single unnamed vector.
            await this._qdrantClient.CreateCollectionAsync(
                name,
                vectorParams,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Since we are using named vectors, iterate over all vector properties.
            var vectorProperties = vectorStoreRecordDefinition.Properties.Where(x => x is VectorStoreRecordVectorProperty).Select(x => (VectorStoreRecordVectorProperty)x);

            // Map the named vectors to the qdrant config.
            var vectorParamsMap = QdrantVectorStoreCollectionCreateMapping.MapNamedVectors(vectorProperties, new Dictionary<string, string>());

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
    private Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        return this.RunOperationAsync(
            "CollectionExists",
            () => this._qdrantClient.CollectionExistsAsync(name, cancellationToken));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> ListCollectionNamesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var collections = await this.RunOperationAsync(
            "ListCollections",
            () => this._qdrantClient.ListCollectionsAsync(cancellationToken)).ConfigureAwait(false);

        foreach (var collection in collections)
        {
            yield return collection;
        }
    }

    /// <summary>
    /// Run the given operation and wrap any <see cref="RpcException"/> with <see cref="VectorStoreOperationException"/>."/>
    /// </summary>
    /// <typeparam name="T">The response type of the operation.</typeparam>
    /// <param name="operationName">The type of database operation being run.</param>
    /// <param name="operation">The operation to run.</param>
    /// <returns>The result of the operation.</returns>
    private async Task<T> RunOperationAsync<T>(string operationName, Func<Task<T>> operation)
    {
        try
        {
            return await operation.Invoke().ConfigureAwait(false);
        }
        catch (RpcException ex)
        {
            throw new VectorStoreOperationException("Call to vector store failed.", ex)
            {
                VectorStoreType = DatabaseName,
                OperationName = operationName
            };
        }
    }
}
