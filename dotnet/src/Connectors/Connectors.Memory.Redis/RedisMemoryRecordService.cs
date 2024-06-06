// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Memory;
using NRedisStack.Json.DataTypes;
using NRedisStack.RedisStackCommands;
using StackExchange.Redis;

namespace Microsoft.SemanticKernel.Connectors.Redis;

/// <summary>
/// Service for storing and retrieving memory records, that uses Redis as the underlying storage.
/// </summary>
/// <typeparam name="TDataModel">The data model to use for adding, updating and retrieving data from storage.</typeparam>
public sealed class RedisMemoryRecordService<TDataModel> : IMemoryRecordService<string, TDataModel>
    where TDataModel : class
{
    /// <summary>A set of types that a key on the provided model may have.</summary>
    private static readonly HashSet<Type> s_supportedKeyTypes = new()
    {
        typeof(string)
    };

    /// <summary>A set of types that vectors on the provided model may have.</summary>
    private static readonly HashSet<Type> s_supportedVectorTypes = new()
    {
        typeof(ReadOnlyMemory<float>),
        typeof(ReadOnlyMemory<double>),
        typeof(ReadOnlyMemory<float>?),
        typeof(ReadOnlyMemory<double>?)
    };

    /// <summary>The redis database to read/write records from.</summary>
    private readonly IDatabase _database;

    /// <summary>The name of the collection to use with this store if none is provided for any individual operation.</summary>
    private readonly string _defaultCollectionName;

    /// <summary>Optional configuration options for this class.</summary>
    private readonly RedisMemoryRecordServiceOptions<TDataModel> _options;

    /// <summary>A property info object that points at the key property for the current model, allowing easy reading and writing of this property.</summary>
    private readonly PropertyInfo _keyPropertyInfo;

    /// <summary>The name of the temporary json property that the key property will be serialized / parsed from.</summary>
    private readonly string _keyJsonPropertyName;

    /// <summary>An array of the names of all the data properties that are part of the redis payload, i.e. all properties except the vector properties.</summary>
    private readonly string[] _dataPropertyNames;

    /// <summary>An array of the names of all the data and vector properties that are part of the redis payload.</summary>
    private readonly string[] _dataAndVectorPropertyNames;

    /// <summary>The mapper to use when mapping between the consumer data model and the redis record.</summary>
    private readonly IMemoryRecordMapper<TDataModel, (string Key, JsonNode Node)> _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisMemoryRecordService{TDataModel}"/> class.
    /// </summary>
    /// <param name="database">The redis database to read/write records from.</param>
    /// <param name="defaultCollectionName">The name of the collection to use with this store if none is provided for any individual operation.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    /// <exception cref="ArgumentNullException">Throw when parameters are invalid.</exception>
    public RedisMemoryRecordService(IDatabase database, string defaultCollectionName, RedisMemoryRecordServiceOptions<TDataModel>? options)
    {
        // Verify.
        Verify.NotNull(database);
        Verify.NotNullOrWhiteSpace(defaultCollectionName);

        // Assign.
        this._database = database;
        this._defaultCollectionName = defaultCollectionName;
        this._options = options ?? new RedisMemoryRecordServiceOptions<TDataModel>();

        // Enumerate public properties on model and store for later use.
        var properties = MemoryServiceModelPropertyReader.FindProperties(typeof(TDataModel), true);
        MemoryServiceModelPropertyReader.VerifyPropertyTypes([properties.keyProperty], s_supportedKeyTypes, "Key");
        MemoryServiceModelPropertyReader.VerifyPropertyTypes(properties.vectorProperties, s_supportedVectorTypes, "Vector");

        this._keyPropertyInfo = properties.keyProperty;
        this._keyJsonPropertyName = MemoryServiceModelPropertyReader.GetSerializedPropertyName(this._keyPropertyInfo);

        this._dataPropertyNames = properties
            .dataProperties
            .Select(MemoryServiceModelPropertyReader.GetSerializedPropertyName)
            .ToArray();

        this._dataAndVectorPropertyNames = this._dataPropertyNames
            .Concat(properties.vectorProperties.Select(MemoryServiceModelPropertyReader.GetSerializedPropertyName))
            .ToArray();

        // Assign Mapper.
        if (this._options.MapperType == RedisMemoryRecordMapperType.JsonNodeCustomMapper)
        {
            if (this._options.JsonNodeCustomMapper is null)
            {
                throw new ArgumentException($"The {nameof(RedisMemoryRecordServiceOptions<TDataModel>.JsonNodeCustomMapper)} option needs to be set if a {nameof(RedisMemoryRecordServiceOptions<TDataModel>.MapperType)} of {nameof(RedisMemoryRecordMapperType.JsonNodeCustomMapper)} has been chosen.", nameof(options));
            }

            this._mapper = this._options.JsonNodeCustomMapper;
        }
        else
        {
            this._mapper = new RedisMemoryRecordMapper<TDataModel>(this._keyJsonPropertyName);
        }
    }

    /// <inheritdoc />
    public async Task<TDataModel?> GetAsync(string key, GetRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        Verify.NotNullOrWhiteSpace(key);

        // Create Options
        var collectionName = options?.CollectionName ?? this._defaultCollectionName;
        var maybePrefixedKey = this.PrefixKeyIfNeeded(key, collectionName);

        // Get the redis value.
        var redisResult = options?.IncludeVectors is true ?
            await this._database
                .JSON()
                .GetAsync(maybePrefixedKey).ConfigureAwait(false) :
            await this._database
                .JSON()
                .GetAsync(maybePrefixedKey, this._dataPropertyNames).ConfigureAwait(false);

        // Check if the key was found before trying to serialize the result.
        if (redisResult.IsNull)
        {
            throw new HttpOperationException($"Could not find document with key '{key}'");
        }

        var node = JsonSerializer.Deserialize<JsonNode>(redisResult.ToString())!;
        return this._mapper.MapFromStorageToDataModel((key, node));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TDataModel?> GetBatchAsync(IEnumerable<string> keys, GetRecordOptions? options = default, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Verify.NotNull(keys);

        var keysList = keys.ToList();

        // Create Options
        var collectionName = options?.CollectionName ?? this._defaultCollectionName;
        var maybePrefixedKeys = keysList.Select(key => this.PrefixKeyIfNeeded(key, collectionName));

        // Get the list of redis results.
        var redisResults = await this._database
            .JSON()
            .MGetAsync(maybePrefixedKeys.Select(x => new RedisKey(x)).ToArray(), "$").ConfigureAwait(false);

        // Loop through each key and result and convert to the caller's data model.
        for (int i = 0; i < keysList.Count; i++)
        {
            var key = keysList[i];
            var redisResult = redisResults[i];

            // Check if the key was found before trying to serialize the result.
            if (redisResult.IsNull)
            {
                throw new HttpOperationException(HttpStatusCode.NotFound, null, null, null);
            }

            var node = JsonSerializer.Deserialize<JsonNode>(redisResult.ToString())!;
            yield return this._mapper.MapFromStorageToDataModel((key, node));
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string key, DeleteRecordOptions? options = default, CancellationToken cancellationToken = default)
    {
        Verify.NotNullOrWhiteSpace(key);

        // Create Options
        var collectionName = options?.CollectionName ?? this._defaultCollectionName;
        var maybePrefixedKey = this.PrefixKeyIfNeeded(key, collectionName);

        // Remove.
        await this._database
            .JSON()
            .DelAsync(maybePrefixedKey).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteBatchAsync(IEnumerable<string> keys, DeleteRecordOptions? options = default, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(keys);

        // Remove records in parallel.
        var tasks = keys.Select(key => this.DeleteAsync(key, options, cancellationToken));
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string> UpsertAsync(TDataModel record, UpsertRecordOptions? options = default, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(record);

        // Create Options
        var collectionName = options?.CollectionName ?? this._defaultCollectionName;

        // Map.
        var redisJsonRecord = this._mapper.MapFromDataToStorageModel(record);

        // Upsert.
        var maybePrefixedKey = this.PrefixKeyIfNeeded(redisJsonRecord.Key, collectionName);
        await this._database
            .JSON()
            .SetAsync(
                maybePrefixedKey,
                "$",
                redisJsonRecord.Node).ConfigureAwait(false);

        return redisJsonRecord.Key;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> UpsertBatchAsync(IEnumerable<TDataModel> records, UpsertRecordOptions? options = default, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Verify.NotNull(records);

        // Create Options
        var collectionName = options?.CollectionName ?? this._defaultCollectionName;

        // Map.
        var redisRecords = new List<(string maybePrefixedKey, string originalKey, JsonNode jsonNode)>();
        foreach (var record in records)
        {
            var redisJsonRecord = this._mapper.MapFromDataToStorageModel(record);
            var maybePrefixedKey = this.PrefixKeyIfNeeded(redisJsonRecord.Key, collectionName);
            redisRecords.Add((maybePrefixedKey, redisJsonRecord.Key, redisJsonRecord.Node));
        }

        // Upsert.
        await this._database
            .JSON()
            .MSetAsync(redisRecords.Select(x => new KeyPathValue(x.maybePrefixedKey, "$", x.jsonNode)).ToArray()).ConfigureAwait(false);

        // Return keys of upserted records.
        foreach (var record in redisRecords)
        {
            yield return record.originalKey;
        }
    }

    /// <summary>
    /// Prefix the key with the collection name if the option is set.
    /// </summary>
    /// <param name="key">The key to prefix.</param>
    /// <param name="operationCollectionName">The optional collection name that may have been provided as part of an operation to override the default.</param>
    /// <returns>The updated key if updating is required, otherwise the input key.</returns>
    private string PrefixKeyIfNeeded(string key, string? operationCollectionName)
    {
        if (this._options.PrefixCollectionNameToKeyNames)
        {
            var collectionName = operationCollectionName ?? this._defaultCollectionName;
            return $"{collectionName}:{key}";
        }

        return key;
    }
}
