// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Memory;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Microsoft.SemanticKernel.Connectors.Qdrant;

/// <summary>
/// Class that can create a new collection in qdrant using a provided configuration.
/// </summary>
public sealed class QdrantVectorCollectionCreate : IVectorCollectionCreate, IConfiguredVectorCollectionCreate
{
    /// <summary>Qdrant client that can be used to manage the collections and points in a Qdrant store.</summary>
    private readonly QdrantClient _qdrantClient;

    /// <summary>Defines the schema of the record type and is used to create the collection with.</summary>
    private readonly VectorStoreRecordDefinition? _vectorStoreRecordDefinition;

    /// <summary>Options that modify the behavior of create.</summary>
    private readonly QdrantVectorCollectionCreateOptions _options;

    /// <summary>A dictionary of types and their matching qdrant index schema type.</summary>
    private static readonly Dictionary<Type, PayloadSchemaType> s_schemaTypeMap = new()
    {
        { typeof(short), PayloadSchemaType.Integer },
        { typeof(sbyte), PayloadSchemaType.Integer },
        { typeof(byte), PayloadSchemaType.Integer },
        { typeof(ushort), PayloadSchemaType.Integer },
        { typeof(int), PayloadSchemaType.Integer },
        { typeof(uint), PayloadSchemaType.Integer },
        { typeof(long), PayloadSchemaType.Integer },
        { typeof(ulong), PayloadSchemaType.Integer },
        { typeof(float), PayloadSchemaType.Float },
        { typeof(double), PayloadSchemaType.Float },
        { typeof(decimal), PayloadSchemaType.Float },

        { typeof(short?), PayloadSchemaType.Integer },
        { typeof(sbyte?), PayloadSchemaType.Integer },
        { typeof(byte?), PayloadSchemaType.Integer },
        { typeof(ushort?), PayloadSchemaType.Integer },
        { typeof(int?), PayloadSchemaType.Integer },
        { typeof(uint?), PayloadSchemaType.Integer },
        { typeof(long?), PayloadSchemaType.Integer },
        { typeof(ulong?), PayloadSchemaType.Integer },
        { typeof(float?), PayloadSchemaType.Float },
        { typeof(double?), PayloadSchemaType.Float },
        { typeof(decimal?), PayloadSchemaType.Float },

        { typeof(string), PayloadSchemaType.Text },
        { typeof(DateTime), PayloadSchemaType.Datetime },
        { typeof(bool), PayloadSchemaType.Bool },

        { typeof(DateTime?), PayloadSchemaType.Datetime },
        { typeof(bool?), PayloadSchemaType.Bool },
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="QdrantVectorCollectionCreate"/> class.
    /// </summary>
    /// <param name="qdrantClient">Qdrant client that can be used to manage the collections and points in a Qdrant store.</param>
    /// <param name="vectorStoreRecordDefinition">Defines the schema of the record type and is used to create the collection with.</param>
    /// <param name="options">Options that modify the behavior of create.</param>
    private QdrantVectorCollectionCreate(QdrantClient qdrantClient, VectorStoreRecordDefinition vectorStoreRecordDefinition, QdrantVectorCollectionCreateOptions options)
    {
        Verify.NotNull(qdrantClient);
        Verify.NotNull(vectorStoreRecordDefinition);
        Verify.NotNull(options);

        this._qdrantClient = qdrantClient;
        this._vectorStoreRecordDefinition = vectorStoreRecordDefinition;
        this._options = options;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QdrantVectorCollectionCreate"/> class.
    /// </summary>
    /// <param name="qdrantClient">Qdrant client that can be used to manage the collections and points in a Qdrant store.</param>
    /// <param name="options">Options that modify the behavior of create.</param>
    private QdrantVectorCollectionCreate(QdrantClient qdrantClient, QdrantVectorCollectionCreateOptions options)
    {
        Verify.NotNull(qdrantClient);
        Verify.NotNull(options);

        this._qdrantClient = qdrantClient;
        this._options = options;
    }

    /// <summary>
    /// Create a new instance of <see cref="IVectorCollectionCreate"/> using the provided <see cref="VectorStoreRecordDefinition"/>.
    /// </summary>
    /// <param name="qdrantClient">Qdrant client that can be used to manage the collections and points in a Qdrant store.</param>
    /// <param name="vectorStoreRecordDefinition">Defines the schema of the record type and is used to create the collection with.</param>
    /// <param name="options">Optional options that modify the behavior of create.</param>
    /// <returns>The new <see cref="IVectorCollectionCreate"/>.</returns>
    public static IVectorCollectionCreate Create(QdrantClient qdrantClient, VectorStoreRecordDefinition vectorStoreRecordDefinition, QdrantVectorCollectionCreateOptions? options = null)
    {
        Verify.NotNull(qdrantClient);
        Verify.NotNull(vectorStoreRecordDefinition);

        var localOptions = options is null ? new QdrantVectorCollectionCreateOptions() : options;
        if (!localOptions.HasNamedVectors && vectorStoreRecordDefinition.Properties.Where(x => x is VectorStoreRecordVectorProperty).Count() > 1)
        {
            throw new ArgumentException($"Multiple vectors per point are not supported when the {nameof(QdrantVectorCollectionCreateOptions.HasNamedVectors)} option is false.", nameof(options));
        }

        return new QdrantVectorCollectionCreate(qdrantClient, vectorStoreRecordDefinition, localOptions);
    }

    /// <summary>
    /// Create a new instance of <see cref="IVectorCollectionCreate"/> by inferring the schema from the provided type and its attributes.
    /// </summary>
    /// <typeparam name="T">The data type to create a collection for.</typeparam>
    /// <param name="qdrantClient">Qdrant client that can be used to manage the collections and points in a Qdrant store.</param>
    /// <param name="options">Optional options that modify the behavior of create.</param>
    /// <returns>The new <see cref="IVectorCollectionCreate"/>.</returns>
    public static IVectorCollectionCreate Create<T>(QdrantClient qdrantClient, QdrantVectorCollectionCreateOptions? options = null)
    {
        Verify.NotNull(qdrantClient);

        var localOptions = options is null ? new QdrantVectorCollectionCreateOptions() : options;
        var vectorStoreRecordDefinition = VectorStoreRecordPropertyReader.CreateVectorStoreRecordDefinitionFromType(typeof(T), localOptions.HasNamedVectors);
        return new QdrantVectorCollectionCreate(qdrantClient, vectorStoreRecordDefinition, localOptions);
    }

    /// <summary>
    /// Create a new instance of <see cref="IConfiguredVectorCollectionCreate"/>.
    /// </summary>
    /// <param name="qdrantClient">Qdrant client that can be used to manage the collections and points in a Qdrant store.</param>
    /// <param name="options">Optional options that modify the behavior of create.</param>
    /// <returns>The new <see cref="IConfiguredVectorCollectionCreate"/>.</returns>
    public static IConfiguredVectorCollectionCreate Create(QdrantClient qdrantClient, QdrantVectorCollectionCreateOptions? options = null)
    {
        Verify.NotNull(qdrantClient);

        return new QdrantVectorCollectionCreate(qdrantClient, options is null ? new QdrantVectorCollectionCreateOptions() : options);
    }

    /// <inheritdoc />
    public Task CreateCollectionAsync(string name, CancellationToken cancellationToken = default)
    {
        if (this._vectorStoreRecordDefinition is null)
        {
            throw new InvalidOperationException($"Cannot create a collection without a {nameof(VectorStoreRecordDefinition)}.");
        }

        return this.CreateCollectionAsync(name, this._vectorStoreRecordDefinition, cancellationToken);
    }

    /// <inheritdoc />
    public async Task CreateCollectionAsync(string name, VectorStoreRecordDefinition vectorStoreRecordDefinition, CancellationToken cancellationToken = default)
    {
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
                new VectorParams { Size = (ulong)singleVectorProperty.Dimensions, Distance = GetSDKDistanceAlgorithm(singleVectorProperty) },
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
                        Distance = GetSDKDistanceAlgorithm(vectorProperty)
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
                s_schemaTypeMap[dataProperty.PropertyType!],
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public Task CreateCollectionAsync<TRecord>(string name, CancellationToken cancellationToken = default)
    {
        var vectorStoreRecordDefinition = VectorStoreRecordPropertyReader.CreateVectorStoreRecordDefinitionFromType(typeof(TRecord), true);
        return this.CreateCollectionAsync(name, vectorStoreRecordDefinition, cancellationToken);
    }

    /// <summary>
    /// Get the configured <see cref="Distance"/> from the given <paramref name="vectorProperty"/>.
    /// If none is configured, the default is <see cref="Distance.Cosine"/>.
    /// </summary>
    /// <param name="vectorProperty">The vector property definition.</param>
    /// <returns>The chosen <see cref="Distance"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if a distance function is chosen that isn't suported by Azure AI Search.</exception>
    private static Distance GetSDKDistanceAlgorithm(VectorStoreRecordVectorProperty vectorProperty)
    {
        if (vectorProperty.DistanceFunction is null)
        {
            return Distance.Cosine;
        }

        return vectorProperty.DistanceFunction switch
        {
            DistanceFunction.CosineSimilarity => Distance.Cosine,
            DistanceFunction.DotProductSimilarity => Distance.Dot,
            DistanceFunction.EuclideanDistance => Distance.Euclid,
            DistanceFunction.ManhattanDistance => Distance.Manhattan,
            _ => throw new InvalidOperationException($"Unsupported distance function '{vectorProperty.DistanceFunction}' for {nameof(VectorStoreRecordVectorProperty)} '{vectorProperty.PropertyName}'.")
        };
    }
}
