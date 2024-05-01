// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Reflection;
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
    /// <summary>A property info object that points at the key field for the current model, allowing easy reading and writing of this property.</summary>
    private readonly PropertyInfo _keyFieldPropertyInfo;

    private readonly IDatabase _database;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisVectorStore{TDataModel}"/> class.
    /// </summary>
    /// <param name="database">The redis database to read/write records from.</param>
    /// <exception cref="ArgumentNullException">Throw when parameters are invalid.</exception>
    public RedisVectorStore(IDatabase database)
    {
        this._database = database ?? throw new ArgumentNullException(nameof(database));
        this._keyFieldPropertyInfo = FindKeyField(typeof(TDataModel));
    }

    /// <inheritdoc />
    public async Task<TDataModel?> GetAsync(string collectionName, string key, VectorStoreGetDocumentOptions? options = null, CancellationToken cancellationToken = default)
    {
        var result = await this._database.JSON().GetAsync<TDataModel>(key).ConfigureAwait(false);
        if (result == null)
        {
            throw new HttpOperationException($"Could not find document with key '{key}'");
        }
        return result;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TDataModel> GetBatchAsync(string collectionName, IEnumerable<string> keys, VectorStoreGetDocumentOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public async Task<string> RemoveAsync(string collectionName, string key, CancellationToken cancellationToken = default)
    {
        await this._database.JSON().DelAsync(key).ConfigureAwait(false);
        return key;
    }

    /// <inheritdoc />
    public Task RemoveBatchAsync(string collectionName, IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public async Task<string> UpsertAsync(string collectionName, TDataModel record, CancellationToken cancellationToken = default)
    {
        var keyValue = (string)this._keyFieldPropertyInfo.GetValue(record);
        await this._database.JSON().SetAsync(keyValue, "$", record).ConfigureAwait(false);
        return keyValue;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<string> UpsertBatchAsync(string collectionName, IEnumerable<TDataModel> records, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
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
