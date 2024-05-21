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
/// Vector store that uses Redis as the underlying storage.
/// </summary>
/// <typeparam name="TDataModel">The data model to use for adding, updating and retrieving data from storage.</typeparam>
public class RedisVectorDBRecordService<TDataModel> : IVectorDBRecordService<string, TDataModel>
    where TDataModel : class
{
    /// <summary>A set of types that a key on the provided model may have.</summary>
    private static readonly HashSet<Type> s_supportedKeyTypes = new()
    {
        typeof(string)
    };

    /// <summary>The redis database to read/write records from.</summary>
    private readonly IDatabase _database;

    /// <summary>The name of the collection to use with this store if none is provided for any individual operation.</summary>
    private readonly string _defaultCollectionName;

    /// <summary>Optional configuration options for this class.</summary>
    private readonly RedisVectorDBRecordServiceOptions _options;

    /// <summary>A property info object that points at the key field for the current model, allowing easy reading and writing of this property.</summary>
    private readonly PropertyInfo _keyFieldPropertyInfo;

    /// <summary>The name of the temporary json property that the key field will be serialized / parsed from.</summary>
    private readonly string _keyFieldJsonPropertyName;

    /// <summary>An array of the names of all the data and metadata properties that are part of the redis payload, i.e. all fields except the vector fields.</summary>
    private readonly string[] _dataAndMetadataFieldNames;

    /// <summary>An array of the names of all the data, metadata and vector properties that are part of the redis payload.</summary>
    private readonly string[] _dataAndMetadataAndVectorFieldNames;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisVectorDBRecordService{TDataModel}"/> class.
    /// </summary>
    /// <param name="database">The redis database to read/write records from.</param>
    /// <param name="defaultCollectionName">The name of the collection to use with this store if none is provided for any individual operation.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    /// <exception cref="ArgumentNullException">Throw when parameters are invalid.</exception>
    public RedisVectorDBRecordService(IDatabase database, string defaultCollectionName, RedisVectorDBRecordServiceOptions? options)
    {
        // Verify.
        Verify.NotNull(database);
        Verify.NotNullOrWhiteSpace(defaultCollectionName);

        // Assign.
        this._database = database;
        this._defaultCollectionName = defaultCollectionName;
        this._options = options ?? new RedisVectorDBRecordServiceOptions();

        // Enumerate public properties/fields on model and store for later use.
        var fields = VectorStoreModelPropertyReader.FindFields(typeof(TDataModel), true);
        VectorStoreModelPropertyReader.VerifyFieldTypes([fields.keyField], s_supportedKeyTypes, "Key");

        this._keyFieldPropertyInfo = fields.keyField;
        this._keyFieldJsonPropertyName = VectorStoreModelPropertyReader.GetSerializedPropertyName(this._keyFieldPropertyInfo);

        this._dataAndMetadataFieldNames = fields
            .dataFields
            .Concat(fields.metadataFields)
            .Select(VectorStoreModelPropertyReader.GetSerializedPropertyName)
            .ToArray();

        this._dataAndMetadataAndVectorFieldNames = this._dataAndMetadataFieldNames
            .Concat(fields.vectorFields.Select(VectorStoreModelPropertyReader.GetSerializedPropertyName))
            .ToArray();
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
                .GetAsync(maybePrefixedKey, this._dataAndMetadataFieldNames).ConfigureAwait(false);

        // Check if the key was found before trying to serialize the result.
        if (redisResult.IsNull)
        {
            throw new HttpOperationException($"Could not find document with key '{key}'");
        }

        var node = JsonSerializer.Deserialize<JsonNode>(redisResult.ToString());
        return this.MapFromRedisJsonToDataModel(key, node);
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

            var node = JsonSerializer.Deserialize<JsonNode>(redisResult.ToString());
            yield return this.MapFromRedisJsonToDataModel(key, node);
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, RemoveRecordOptions? options = default, CancellationToken cancellationToken = default)
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
    public async Task RemoveBatchAsync(IEnumerable<string> keys, RemoveRecordOptions? options = default, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(keys);

        foreach (var key in keys)
        {
            await this.RemoveAsync(key, options, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<string> UpsertAsync(TDataModel record, UpsertRecordOptions? options = default, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(record);

        // Create Options
        var collectionName = options?.CollectionName ?? this._defaultCollectionName;

        // Map.
        var redisJsonRecord = this.MapFromDataModelToRedisJson(record);

        // Upsert.
        var maybePrefixedKey = this.PrefixKeyIfNeeded(redisJsonRecord.key, collectionName);
        await this._database
            .JSON()
            .SetAsync(
                maybePrefixedKey,
                "$",
                redisJsonRecord.jsonNode).ConfigureAwait(false);

        return redisJsonRecord.key;
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
            var redisJsonRecord = this.MapFromDataModelToRedisJson(record);
            var maybePrefixedKey = this.PrefixKeyIfNeeded(redisJsonRecord.key, collectionName);
            redisRecords.Add((maybePrefixedKey, redisJsonRecord.key, redisJsonRecord.jsonNode));
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

    /// <summary>
    /// Map from the consumer data model to a redis key and json object.
    /// </summary>
    /// <param name="record">The consumer record to map.</param>
    /// <returns>The mapped result.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    private (string key, JsonNode jsonNode) MapFromDataModelToRedisJson(TDataModel record)
    {
        // Convert the provided record into a JsonNode object and try to get the key field for it.
        // Since we aleady checked that the key field is a string in the constructor, and that it exists on the model,
        // the only edge case we have to be concerned about is if the key field is null.
        var jsonNode = JsonSerializer.SerializeToNode(record);
        if (jsonNode!.AsObject().TryGetPropertyValue(this._keyFieldPropertyInfo.Name, out var keyField) && keyField is JsonValue jsonValue)
        {
            // Remove the key field from the JSON object since we don't want to store it in the redis payload.
            var keyValue = jsonValue.ToString();
            jsonNode.AsObject().Remove(this._keyFieldPropertyInfo.Name);

            return (keyValue, jsonNode);
        }

        throw new InvalidOperationException($"Missing key field {this._keyFieldPropertyInfo.Name} on provided record of type {typeof(TDataModel).FullName}.");
    }

    /// <summary>
    /// Map from the redis key and json object to the consumer data model.
    /// </summary>
    /// <param name="key">The key of the redis json object.</param>
    /// <param name="jsonNode">The redis json object.</param>
    /// <returns>The mapped result.</returns>
    /// <exception cref="HttpOperationException"></exception>
    private TDataModel? MapFromRedisJsonToDataModel(string key, JsonNode? jsonNode)
    {
        JsonObject jsonObject;

        // The redis result can be either a single object or an array with a single object in the case where we are doing an MGET.
        if (jsonNode is JsonObject topLevelJsonObject)
        {
            jsonObject = topLevelJsonObject;
        }
        else if (jsonNode is JsonArray jsonArray && jsonArray.Count == 1 && jsonArray[0] is JsonObject arrayEntryJsonObject)
        {
            jsonObject = arrayEntryJsonObject;
        }
        else
        {
            throw new HttpOperationException($"Invalid data format for document with key '{key}'");
        }

        // Check that the key field is not already present in the redis value.
        if (jsonObject.ContainsKey(this._keyFieldPropertyInfo.Name))
        {
            throw new HttpOperationException($"Invalid data format for document with key '{key}'. Key property '{this._keyFieldPropertyInfo.Name}' already present on retrieved object.");
        }

        // Since the key is not stored in the redis value, add it back in before deserializing into the data model.
        jsonObject.Add(this._keyFieldPropertyInfo.Name, key);

        return JsonSerializer.Deserialize<TDataModel>(jsonObject);
    }
}
