// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Memory;
using NRedisStack.RedisStackCommands;
using StackExchange.Redis;

namespace Microsoft.SemanticKernel.Connectors.Redis;

/// <summary>
/// Vector store that uses Redis as the underlying storage.
/// </summary>
/// <typeparam name="TDataModel">The data model to use for adding, updating and retrieving data from storage.</typeparam>
public class RedisVectorStore<TDataModel> : IVectorStore<TDataModel>
    where TDataModel : class
{
    /// <summary>The redis database to read/write records from.</summary>
    private readonly IDatabase _database;

    /// <summary>The name of the collection to use with this store if none is provided for any individual operation.</summary>
    private readonly string _defaultCollectionName;

    /// <summary>Optional configuration options for this class.</summary>
    private readonly RedisVectorStoreOptions _options;

    /// <summary>A property info object that points at the key field for the current model, allowing easy reading and writing of this property.</summary>
    private readonly PropertyInfo _keyFieldPropertyInfo;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisVectorStore{TDataModel}"/> class.
    /// </summary>
    /// <param name="database">The redis database to read/write records from.</param>
    /// <param name="defaultCollectionName">The name of the collection to use with this store if none is provided for any individual operation.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    /// <exception cref="ArgumentNullException">Throw when parameters are invalid.</exception>
    public RedisVectorStore(IDatabase database, string defaultCollectionName, RedisVectorStoreOptions? options)
    {
        this._database = database ?? throw new ArgumentNullException(nameof(database));
        this._defaultCollectionName = string.IsNullOrWhiteSpace(defaultCollectionName) ? throw new ArgumentException("Default collection name is required.", nameof(defaultCollectionName)) : defaultCollectionName;
        this._options = options ?? new RedisVectorStoreOptions();
        this._keyFieldPropertyInfo = FindKeyField(typeof(TDataModel));
    }

    /// <inheritdoc />
    public async Task<TDataModel?> GetAsync(string key, VectorStoreGetDocumentOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        // Get the redis value and parse the JSON stored in Redis into a JsonNode object.
        var maybePrefixedKey = this.PrefixKeyIfNeeded(key, options?.CollectionName);
        var redisResult = await this._database
            .JSON()
            .GetAsync(maybePrefixedKey).ConfigureAwait(false);

        // Check if the key was found before trying to serialize the result.
        if (redisResult.IsNull)
        {
            throw new HttpOperationException($"Could not find document with key '{key}'");
        }

        var node = JsonSerializer.Deserialize<JsonNode>(redisResult.ToString());

        // Since the key is not stored in the redis value, add it back in before deserializing into the data model.
        if (node is JsonObject jsonObject)
        {
            if (jsonObject.ContainsKey(this._keyFieldPropertyInfo.Name))
            {
                throw new HttpOperationException($"Invalid data format for document with key '{key}'");
            }

            jsonObject.Add(this._keyFieldPropertyInfo.Name, key);
            return JsonSerializer.Deserialize<TDataModel>(jsonObject);
        }

        throw new HttpOperationException($"Invalid data format for document with key '{key}'");
    }

    /// <inheritdoc />
    public async Task<string> RemoveAsync(string key, VectorStoreRemoveDocumentOptions? options = default, CancellationToken cancellationToken = default)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        var maybePrefixedKey = this.PrefixKeyIfNeeded(key, options?.CollectionName);
        await this._database
            .JSON()
            .DelAsync(maybePrefixedKey).ConfigureAwait(false);

        return key;
    }

    /// <inheritdoc />
    public async Task<string> UpsertAsync(TDataModel record, VectorStoreUpsertDocumentOptions? options = default, CancellationToken cancellationToken = default)
    {
        if (record is null)
        {
            throw new ArgumentNullException(nameof(record));
        }

        // Convert the provided record into a JsonNode object and try to get the key field for it.
        // Since we aleady checked that the key field is a string in the constructor, and that it exists on the model,
        // the only edge case we have to be concerned about is if the key field is null.
        var jsonNode = JsonSerializer.SerializeToNode(record);
        if (jsonNode!.AsObject().TryGetPropertyValue(this._keyFieldPropertyInfo.Name, out var keyField) && keyField is JsonValue jsonValue)
        {
            // Remove the key field from the JSON object since we don't want to store it in the redis payload.
            var keyValue = jsonValue.ToString();
            jsonNode.AsObject().Remove(this._keyFieldPropertyInfo.Name);

            // Prefix the key with the collection name if the option is set and save the JSON object to redis under this key.
            var maybePrefixedKey = this.PrefixKeyIfNeeded(keyValue, options?.CollectionName);
            await this._database
                .JSON()
                .SetAsync(
                    maybePrefixedKey,
                    "$",
                    jsonNode).ConfigureAwait(false);
            return keyValue;
        }

        throw new HttpOperationException($"Missing key field {this._keyFieldPropertyInfo.Name} on provided record of type {typeof(TDataModel).FullName}.");
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
    /// Find the field with the <see cref="VectorStoreModelKeyAttribute"/> and return the property info.
    /// Throws if the key field is not found, if there are multiple key fields, or if the key field is not a string.
    /// </summary>
    /// <param name="type">The data model to find the key field on.</param>
    /// <returns>The key field if found.</returns>
    private static PropertyInfo FindKeyField(Type type)
    {
        PropertyInfo? keyField = null;
        foreach (var property in type.GetProperties())
        {
            if (property.GetCustomAttribute<VectorStoreModelKeyAttribute>() is not null)
            {
                if (keyField is not null)
                {
                    throw new ArgumentException($"Multiple key fields found on type {type.FullName}.");
                }

                if (property.PropertyType != typeof(string))
                {
                    throw new ArgumentException($"Key field must be of type string. Type of {property.Name} is {property.PropertyType.FullName}.");
                }

                keyField = property;
            }
        }

        if (keyField is null)
        {
            throw new ArgumentException($"No key field found on type {type.FullName}.");
        }

        return keyField;
    }
}
