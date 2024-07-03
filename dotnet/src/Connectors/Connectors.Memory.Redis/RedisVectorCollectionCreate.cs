// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Data;
using NRedisStack.RedisStackCommands;
using NRedisStack.Search;
using StackExchange.Redis;

namespace Microsoft.SemanticKernel.Connectors.Redis;

/// <summary>
/// Class that can create a new collection in redis using a provided configuration.
/// </summary>
public sealed class RedisVectorCollectionCreate : IVectorCollectionCreate, IConfiguredVectorCollectionCreate
{
    /// <summary>The redis database to read/write indices from.</summary>
    private readonly IDatabase _database;

    /// <summary>Defines the schema of the record type and is used to create the collection with.</summary>
    private readonly VectorStoreRecordDefinition? _vectorStoreRecordDefinition;

    /// <summary>A set of number types that are supported for filtering.</summary>
    private static readonly HashSet<Type> s_supportedFilterableNumericDataTypes =
    [
        typeof(short),
        typeof(sbyte),
        typeof(byte),
        typeof(ushort),
        typeof(int),
        typeof(uint),
        typeof(long),
        typeof(ulong),
        typeof(float),
        typeof(double),
        typeof(decimal),

        typeof(short?),
        typeof(sbyte?),
        typeof(byte?),
        typeof(ushort?),
        typeof(int?),
        typeof(uint?),
        typeof(long?),
        typeof(ulong?),
        typeof(float?),
        typeof(double?),
        typeof(decimal?),
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisVectorCollectionCreate"/> class.
    /// </summary>
    /// <param name="database">The redis database to read/write indices from.</param>
    /// <param name="vectorStoreRecordDefinition">Defines the schema of the record type and is used to create the collection with.</param>
    private RedisVectorCollectionCreate(IDatabase database, VectorStoreRecordDefinition vectorStoreRecordDefinition)
    {
        Verify.NotNull(database);
        Verify.NotNull(vectorStoreRecordDefinition);

        this._database = database;
        this._vectorStoreRecordDefinition = vectorStoreRecordDefinition;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisVectorCollectionCreate"/> class.
    /// </summary>
    /// <param name="database">The redis database to read/write indices from.</param>
    private RedisVectorCollectionCreate(IDatabase database)
    {
        Verify.NotNull(database);
        this._database = database;
    }

    /// <summary>
    /// Create a new instance of <see cref="IVectorCollectionCreate"/> using the provided <see cref="VectorStoreRecordDefinition"/>.
    /// </summary>
    /// <param name="database">The redis database to read/write indices from.</param>
    /// <param name="vectorStoreRecordDefinition">Defines the schema of the record type and is used to create the collection with.</param>
    /// <returns>The new <see cref="IVectorCollectionCreate"/>.</returns>
    public static IVectorCollectionCreate Create(IDatabase database, VectorStoreRecordDefinition vectorStoreRecordDefinition)
    {
        return new RedisVectorCollectionCreate(database, vectorStoreRecordDefinition);
    }

    /// <summary>
    /// Create a new instance of <see cref="IVectorCollectionCreate"/> by inferring the schema from the provided type and its attributes.
    /// </summary>
    /// <typeparam name="T">The data type to create a collection for.</typeparam>
    /// <param name="database">The redis database to read/write indices from.</param>
    /// <returns>The new <see cref="IVectorCollectionCreate"/>.</returns>
    public static IVectorCollectionCreate Create<T>(IDatabase database)
    {
        var vectorStoreRecordDefinition = VectorStoreRecordPropertyReader.CreateVectorStoreRecordDefinitionFromType(typeof(T), true);
        return new RedisVectorCollectionCreate(database, vectorStoreRecordDefinition);
    }

    /// <summary>
    /// Create a new instance of <see cref="IConfiguredVectorCollectionCreate"/>.
    /// </summary>
    /// <param name="database">The redis database to read/write indices from.</param>
    /// <returns>The new <see cref="IConfiguredVectorCollectionCreate"/>.</returns>
    public static IConfiguredVectorCollectionCreate Create(IDatabase database)
    {
        return new RedisVectorCollectionCreate(database);
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
    public Task CreateCollectionAsync(string name, VectorStoreRecordDefinition vectorStoreRecordDefinition, CancellationToken cancellationToken = default)
    {
        var schema = new Schema();

        // Loop through all properties and create the index fields.
        foreach (var property in vectorStoreRecordDefinition.Properties)
        {
            // Key property.
            if (property is VectorStoreRecordKeyProperty keyProperty)
            {
                // Do nothing, since key is not stored as part of the payload and therefore doesn't have to be added to the index.
            }

            // Data property.
            if (property is VectorStoreRecordDataProperty dataProperty && dataProperty.IsFilterable)
            {
                if (dataProperty.PropertyType is null)
                {
                    throw new InvalidOperationException($"Property {nameof(dataProperty.PropertyType)} on {nameof(VectorStoreRecordDataProperty)} '{dataProperty.PropertyName}' must be set to create a collection, since the property is filterable.");
                }

                if (dataProperty.PropertyType == typeof(string))
                {
                    schema.AddTextField(dataProperty.PropertyName);
                }

                if (s_supportedFilterableNumericDataTypes.Contains(dataProperty.PropertyType))
                {
                    schema.AddNumericField(dataProperty.PropertyName);
                }
            }

            // Vector property.
            if (property is VectorStoreRecordVectorProperty vectorProperty)
            {
                if (vectorProperty.Dimensions is not > 0)
                {
                    throw new InvalidOperationException($"Property {nameof(vectorProperty.Dimensions)} on {nameof(VectorStoreRecordVectorProperty)} '{vectorProperty.PropertyName}' must be set to a positive ingeteger to create a collection.");
                }

                var indexKind = GetSDKIndexKind(vectorProperty);
                var distanceAlgorithm = GetSDKDistanceAlgorithm(vectorProperty);
                var dimensions = vectorProperty.Dimensions.Value.ToString(CultureInfo.InvariantCulture);
                schema.AddVectorField(vectorProperty.PropertyName, indexKind, new Dictionary<string, object>()
                {
                    ["TYPE"] = "FLOAT32",
                    ["DIM"] = "4",
                    ["DISTANCE_METRIC"] = distanceAlgorithm
                });
            }
        }

        // Create the index.
        var createParams = new FTCreateParams();
        createParams.AddPrefix(name);
        return this._database.FT().CreateAsync(name, createParams, schema);
    }

    /// <inheritdoc />
    public Task CreateCollectionAsync<TRecord>(string name, CancellationToken cancellationToken = default)
    {
        var vectorStoreRecordDefinition = VectorStoreRecordPropertyReader.CreateVectorStoreRecordDefinitionFromType(typeof(TRecord), true);
        return this.CreateCollectionAsync(name, vectorStoreRecordDefinition, cancellationToken);
    }

    /// <summary>
    /// Get the configured <see cref="Schema.VectorField.VectorAlgo"/> from the given <paramref name="vectorProperty"/>.
    /// If none is configured the default is <see cref="Schema.VectorField.VectorAlgo.HNSW"/>.
    /// </summary>
    /// <param name="vectorProperty">The vector property definition.</param>
    /// <returns>The chosen <see cref="Schema.VectorField.VectorAlgo"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if a index type was chosen that isn't supported by Redis.</exception>
    private static Schema.VectorField.VectorAlgo GetSDKIndexKind(VectorStoreRecordVectorProperty vectorProperty)
    {
        if (vectorProperty.IndexKind is null)
        {
            return Schema.VectorField.VectorAlgo.HNSW;
        }

        return vectorProperty.IndexKind switch
        {
            IndexKind.Hnsw => Schema.VectorField.VectorAlgo.HNSW,
            IndexKind.Flat => Schema.VectorField.VectorAlgo.FLAT,
            _ => throw new InvalidOperationException($"Unsupported index kind '{vectorProperty.IndexKind}' for {nameof(VectorStoreRecordVectorProperty)} '{vectorProperty.PropertyName}'.")
        };
    }

    /// <summary>
    /// Get the configured distance metric from the given <paramref name="vectorProperty"/>.
    /// If none is configured, the default is cosine.
    /// </summary>
    /// <param name="vectorProperty">The vector property definition.</param>
    /// <returns>The chosen distance metric.</returns>
    /// <exception cref="InvalidOperationException">Thrown if a distance function is chosen that isn't suported by Redis.</exception>
    private static string GetSDKDistanceAlgorithm(VectorStoreRecordVectorProperty vectorProperty)
    {
        if (vectorProperty.DistanceFunction is null)
        {
            return "COSINE";
        }

        return vectorProperty.DistanceFunction switch
        {
            DistanceFunction.CosineSimilarity => "COSINE",
            DistanceFunction.DotProductSimilarity => "IP",
            DistanceFunction.EuclideanDistance => "L2",
            _ => throw new InvalidOperationException($"Unsupported distance function '{vectorProperty.DistanceFunction}' for {nameof(VectorStoreRecordVectorProperty)} '{vectorProperty.PropertyName}'.")
        };
    }
}
