// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.SemanticKernel.Data;
using StackExchange.Redis;

namespace Microsoft.SemanticKernel.Connectors.Redis;

/// <summary>
/// Class for mapping between a json hashset stored in redis, and the consumer data model.
/// </summary>
/// <typeparam name="TConsumerDataModel">The consumer data model to map to or from.</typeparam>
internal sealed class RedisHashSetVectorStoreRecordMapper<TConsumerDataModel> : IVectorStoreRecordMapper<TConsumerDataModel, (string Key, HashEntry[] HashEntries)>
    where TConsumerDataModel : class
{
    /// <summary>A property info object that points at the key property for the current model, allowing easy reading and writing of this property.</summary>
    private readonly PropertyInfo _keyPropertyInfo;

    /// <summary>The name of the temporary json property that the key field will be serialized / parsed from.</summary>
    private readonly string _keyFieldJsonPropertyName;

    /// <summary>A list of property info objects that point at the data properties in the current model, and allows easy reading and writing of these properties.</summary>
    private readonly IEnumerable<PropertyInfo> _dataPropertiesInfo;

    /// <summary>A list of property info objects that point at the vector properties in the current model, and allows easy reading and writing of these properties.</summary>
    private readonly IEnumerable<PropertyInfo> _vectorPropertiesInfo;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisHashSetVectorStoreRecordMapper{TConsumerDataModel}"/> class.
    /// </summary>
    /// <param name="keyPropertyInfo">The property info object that points at the key property for the current model.</param>
    /// <param name="keyFieldJsonPropertyName">The name of the key field on the model when serialized to json.</param>
    /// <param name="dataPropertiesInfo">The property info objects that point at the payload properties in the current model.</param>
    /// <param name="vectorPropertiesInfo">The property info objects that point at the vector properties in the current model.</param>
    public RedisHashSetVectorStoreRecordMapper(PropertyInfo keyPropertyInfo, string keyFieldJsonPropertyName, IEnumerable<PropertyInfo> dataPropertiesInfo, IEnumerable<PropertyInfo> vectorPropertiesInfo)
    {
        Verify.NotNull(keyPropertyInfo);
        Verify.NotNullOrWhiteSpace(keyFieldJsonPropertyName);
        Verify.NotNull(dataPropertiesInfo);
        Verify.NotNull(vectorPropertiesInfo);

        this._keyPropertyInfo = keyPropertyInfo;
        this._keyFieldJsonPropertyName = keyFieldJsonPropertyName;
        this._dataPropertiesInfo = dataPropertiesInfo;
        this._vectorPropertiesInfo = vectorPropertiesInfo;
    }

    /// <inheritdoc />
    public (string Key, HashEntry[] HashEntries) MapFromDataToStorageModel(TConsumerDataModel dataModel)
    {
        var keyValue = this._keyPropertyInfo.GetValue(dataModel) as string ?? throw new VectorStoreRecordMappingException($"Missing key property {this._keyPropertyInfo.Name} on provided record of type {typeof(TConsumerDataModel).FullName}.");

        var hashEntries = new List<HashEntry>();
        foreach (var property in this._dataPropertiesInfo)
        {
            var value = property.GetValue(dataModel);
            hashEntries.Add(new HashEntry(property.Name, RedisValue.Unbox(value)));
        }

        foreach (var property in this._vectorPropertiesInfo)
        {
            var value = property.GetValue(dataModel);
            if (value is not null)
            {
                if (value is ReadOnlyMemory<float> rom)
                {
                    hashEntries.Add(new HashEntry(property.Name, ConvertVectorToBytes(rom)));
                }
                else if (value is ReadOnlyMemory<double> rod)
                {
                    hashEntries.Add(new HashEntry(property.Name, ConvertVectorToBytes(rod)));
                }
            }
        }

        return (keyValue, hashEntries.ToArray());
    }

    /// <inheritdoc />
    public TConsumerDataModel MapFromStorageToDataModel((string Key, HashEntry[] HashEntries) storageModel, StorageToDataModelMapperOptions options)
    {
        var jsonObject = new JsonObject();

        foreach (var property in this._dataPropertiesInfo)
        {
            var hashEntry = storageModel.HashEntries.FirstOrDefault(x => x.Name == property.Name);
            if (hashEntry.Name.HasValue)
            {
                jsonObject.Add(hashEntry.Name!, JsonValue.Create(Convert.ChangeType(hashEntry.Value, property.PropertyType)));
            }
        }

        if (options.IncludeVectors)
        {
            foreach (var property in this._vectorPropertiesInfo)
            {
                var hashEntry = storageModel.HashEntries.FirstOrDefault(x => x.Name == property.Name);
                if (hashEntry.Name.HasValue)
                {
                    if (property.PropertyType == typeof(ReadOnlyMemory<float>) || property.PropertyType == typeof(ReadOnlyMemory<float>?))
                    {
                        var array = MemoryMarshal.Cast<byte, float>((byte[])hashEntry.Value!).ToArray();
                        jsonObject.Add(hashEntry.Name!, JsonValue.Create(array));
                    }
                    else if (property.PropertyType == typeof(ReadOnlyMemory<double>) || property.PropertyType == typeof(ReadOnlyMemory<double>?))
                    {
                        var array = MemoryMarshal.Cast<byte, double>((byte[])hashEntry.Value!).ToArray();
                        jsonObject.Add(hashEntry.Name!, JsonValue.Create(array));
                    }
                    else
                    {
                        throw new VectorStoreRecordMappingException($"Invalid vector type '{property.PropertyType.Name}' found on property '{property.Name}' on provided record of type '{typeof(TConsumerDataModel).FullName}'. Only float and double vectors are supported.");
                    }
                }
            }
        }

        // Check that the key field is not already present in the redis value.
        if (jsonObject.ContainsKey(this._keyFieldJsonPropertyName))
        {
            throw new VectorStoreRecordMappingException($"Invalid data format for document with key '{storageModel.Key}'. Key property '{this._keyFieldJsonPropertyName}' is already present on retrieved object.");
        }

        // Since the key is not stored in the redis value, add it back in before deserializing into the data model.
        jsonObject.Add(this._keyFieldJsonPropertyName, storageModel.Key);

        return JsonSerializer.Deserialize<TConsumerDataModel>(jsonObject)!;
    }

    private static byte[] ConvertVectorToBytes(ReadOnlyMemory<float> vector)
    {
        return MemoryMarshal.AsBytes(vector.Span).ToArray();
    }

    private static byte[] ConvertVectorToBytes(ReadOnlyMemory<double> vector)
    {
        return MemoryMarshal.AsBytes(vector.Span).ToArray();
    }
}
