// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.SemanticKernel.Memory;
using Qdrant.Client.Grpc;

namespace Microsoft.SemanticKernel.Connectors.Qdrant;

/// <summary>
/// Mapper between a Qdrant record and the consumer data model that uses json as an intermediary to allow supporting a wide range of models.
/// </summary>
/// <typeparam name="TDataModel">The consumer data model to map to or from.</typeparam>
internal sealed class QdrantVectorDBRecordJsonMapper<TDataModel> : IVectorDBRecordMapper<TDataModel, PointStruct>
    where TDataModel : class
{
    /// <summary>A set of types that a key on the provided model may have.</summary>
    private static readonly HashSet<Type> s_supportedKeyTypes = new()
    {
        typeof(ulong),
        typeof(Guid)
    };

    /// <summary>A set of types that fields on the provided model may have.</summary>
    private static readonly HashSet<Type> s_supportedFieldTypes = new()
    {
        typeof(List<string>),
        typeof(List<int>),
        typeof(List<long>),
        typeof(List<float>),
        typeof(List<double>),
        typeof(List<bool>),
        typeof(string),
        typeof(int),
        typeof(long),
        typeof(double),
        typeof(float),
        typeof(bool),
        typeof(int?),
        typeof(long?),
        typeof(double?),
        typeof(float?),
        typeof(bool?)
    };

    /// <summary>A set of types that vectors on the provided model may have.</summary>
    private static readonly HashSet<Type> s_supportedVectorTypes = new()
    {
        typeof(ReadOnlyMemory<float>),
        typeof(ReadOnlyMemory<float>?)
    };

    /// <summary>A list of property info objects that point at the payload fields in the current model, and allows easy reading and writing of these properties.</summary>
    private readonly List<PropertyInfo> _payloadFieldsPropertyInfo = new();

    /// <summary>A list of property info objects that point at the vector fields in the current model, and allows easy reading and writing of these properties.</summary>
    private readonly List<PropertyInfo> _vectorFieldsPropertyInfo = new();

    /// <summary>A property info object that points at the key field for the current model, allowing easy reading and writing of this property.</summary>
    private readonly PropertyInfo _keyFieldPropertyInfo;

    /// <summary>Optional configuration options for this class.</summary>
    private readonly QdrantVectorDBRecordJsonMapperOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="QdrantVectorDBRecordJsonMapper{TDataModel}"/> class.
    /// </summary>
    /// <param name="options">Optional options to use when doing the model conversion.</param>
    public QdrantVectorDBRecordJsonMapper(QdrantVectorDBRecordJsonMapperOptions? options)
    {
        this._options = options ?? new QdrantVectorDBRecordJsonMapperOptions();

        // Enumerate/verify public properties/fields on model.
        var fields = VectorStoreModelPropertyReader.FindFields(typeof(TDataModel), this._options.HasNamedVectors);
        VectorStoreModelPropertyReader.VerifyFieldTypes([fields.keyField], s_supportedKeyTypes, "Key");
        VectorStoreModelPropertyReader.VerifyFieldTypes(fields.dataFields, s_supportedFieldTypes, "Data");
        VectorStoreModelPropertyReader.VerifyFieldTypes(fields.metadataFields, s_supportedFieldTypes, "Metadata");
        VectorStoreModelPropertyReader.VerifyFieldTypes(fields.vectorFields, s_supportedVectorTypes, "Vector");

        // Store properties for later use.
        this._keyFieldPropertyInfo = fields.keyField;
        this._payloadFieldsPropertyInfo = fields.dataFields.Concat(fields.metadataFields).ToList();
        this._vectorFieldsPropertyInfo = fields.vectorFields;
    }

    /// <inheritdoc />
    public PointStruct MapFromDataToStorageModel(TDataModel dataModel)
    {
        PointId pointId;
        if (this._keyFieldPropertyInfo.PropertyType == typeof(ulong))
        {
            var key = this._keyFieldPropertyInfo.GetValue(dataModel) as ulong? ?? throw new ArgumentException($"Missing key field {this._keyFieldPropertyInfo.Name} on provided record of type {typeof(TDataModel).FullName}.", nameof(dataModel));
            pointId = new PointId { Num = key };
        }
        else if (this._keyFieldPropertyInfo.PropertyType == typeof(Guid))
        {
            var key = this._keyFieldPropertyInfo.GetValue(dataModel) as Guid? ?? throw new ArgumentException($"Missing key field {this._keyFieldPropertyInfo.Name} on provided record of type {typeof(TDataModel).FullName}.", nameof(dataModel));
            pointId = new PointId { Uuid = key.ToString("D") };
        }
        else
        {
            throw new InvalidOperationException($"Unsupported key type {this._keyFieldPropertyInfo.PropertyType.FullName}.");
        }

        // Create point.
        var pointStruct = new PointStruct
        {
            Id = pointId,
            Vectors = new Vectors(),
            Payload = { },
        };

        // Add point payload.
        foreach (var payloadFieldPropertyInfo in this._payloadFieldsPropertyInfo)
        {
            var propertyName = VectorStoreModelPropertyReader.GetSerializedPropertyName(payloadFieldPropertyInfo);
            var propertyValue = payloadFieldPropertyInfo.GetValue(dataModel);
            pointStruct.Payload.Add(propertyName, ConvertToGrpcFieldValue(propertyValue));
        }

        // Add vectors.
        if (this._options.HasNamedVectors)
        {
            var namedVectors = new NamedVectors();
            foreach (var vectorFieldPropertyInfo in this._vectorFieldsPropertyInfo)
            {
                var propertyName = VectorStoreModelPropertyReader.GetSerializedPropertyName(vectorFieldPropertyInfo);
                var propertyValue = vectorFieldPropertyInfo.GetValue(dataModel);
                if (propertyValue is not null)
                {
                    var castPropertyValue = (ReadOnlyMemory<float>)propertyValue;
                    namedVectors.Vectors.Add(propertyName, castPropertyValue.ToArray());
                }
            }

            pointStruct.Vectors.Vectors_ = namedVectors;
        }
        else
        {
            var vectorFieldPropertyInfo = this._vectorFieldsPropertyInfo.First();
            var propertyValue = (ReadOnlyMemory<float>)vectorFieldPropertyInfo.GetValue(dataModel);
            pointStruct.Vectors.Vector = propertyValue.ToArray();
        }

        return pointStruct;
    }

    /// <inheritdoc />
    public TDataModel MapFromStorageToDataModel(PointStruct storageModel, GetRecordOptions? options = default)
    {
        // Get the key property name and value.
        var keyPropertyName = VectorStoreModelPropertyReader.GetSerializedPropertyName(this._keyFieldPropertyInfo);
        var keyPropertyValue = storageModel.Id.HasNum ? storageModel.Id.Num as object : storageModel.Id.Uuid as object;

        // Create a json object to represent the point.
        var outputJsonObject = new JsonObject
        {
            { keyPropertyName, JsonValue.Create(keyPropertyValue) },
        };

        // Add each vector field if embeddings are included in the point.
        if (options?.IncludeVectors is true)
        {
            foreach (var vectorProperty in this._vectorFieldsPropertyInfo)
            {
                var propertyName = VectorStoreModelPropertyReader.GetSerializedPropertyName(vectorProperty);

                if (this._options.HasNamedVectors)
                {
                    if (storageModel.Vectors.Vectors_.Vectors.TryGetValue(propertyName, out var vector))
                    {
                        outputJsonObject.Add(propertyName, new JsonArray(vector.Data.Select(x => JsonValue.Create(x)).ToArray()));
                    }
                }
                else
                {
                    outputJsonObject.Add(propertyName, new JsonArray(storageModel.Vectors.Vector.Data.Select(x => JsonValue.Create(x)).ToArray()));
                }
            }
        }

        // Add each payload field.
        foreach (var payloadProperty in this._payloadFieldsPropertyInfo)
        {
            var propertyName = VectorStoreModelPropertyReader.GetSerializedPropertyName(payloadProperty);
            if (storageModel.Payload.TryGetValue(propertyName, out var value))
            {
                outputJsonObject.Add(propertyName, ConvertFromGrpcFieldValueToJsonNode(value));
            }
        }

        // Convert from json object to the target data model.
        return JsonSerializer.Deserialize<TDataModel>(outputJsonObject)!;
    }

    /// <summary>
    /// Convert the given <paramref name="payloadValue"/> to the correct native type based on its properties.
    /// </summary>
    /// <param name="payloadValue">The value to convert to a native type.</param>
    /// <returns>The converted native value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when an unsupported type is enountered.</exception>
    private static JsonNode? ConvertFromGrpcFieldValueToJsonNode(Value payloadValue)
    {
        return payloadValue.KindCase switch
        {
            Value.KindOneofCase.NullValue => null,
            Value.KindOneofCase.IntegerValue => JsonValue.Create(payloadValue.IntegerValue),
            Value.KindOneofCase.StringValue => JsonValue.Create(payloadValue.StringValue),
            Value.KindOneofCase.DoubleValue => JsonValue.Create(payloadValue.DoubleValue),
            Value.KindOneofCase.BoolValue => JsonValue.Create(payloadValue.BoolValue),
            Value.KindOneofCase.ListValue => new JsonArray(payloadValue.ListValue.Values.Select(x => ConvertFromGrpcFieldValueToJsonNode(x)).ToArray()),
            Value.KindOneofCase.StructValue => new JsonObject(payloadValue.StructValue.Fields.ToDictionary(x => x.Key, x => ConvertFromGrpcFieldValueToJsonNode(x.Value))),
            _ => throw new InvalidOperationException($"Unsupported grpc value kind {payloadValue.KindCase}."),
        };
    }

    /// <summary>
    /// Convert the given <paramref name="sourceValue"/> to a <see cref="Value"/> object that can be stored in Qdrant.
    /// </summary>
    /// <param name="sourceValue">The object to convert.</param>
    /// <returns>The converted Qdrant value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when an unsupported type is enountered.</exception>
    private static Value ConvertToGrpcFieldValue(object? sourceValue)
    {
        var value = new Value();
        if (sourceValue is null)
        {
            value.NullValue = NullValue.NullValue;
        }
        else if (sourceValue is int intValue)
        {
            value.IntegerValue = intValue;
        }
        else if (sourceValue is long longValue)
        {
            value.IntegerValue = longValue;
        }
        else if (sourceValue is string stringValue)
        {
            value.StringValue = stringValue;
        }
        else if (sourceValue is float floatValue)
        {
            value.DoubleValue = floatValue;
        }
        else if (sourceValue is double doubleValue)
        {
            value.DoubleValue = doubleValue;
        }
        else if (sourceValue is bool boolValue)
        {
            value.BoolValue = boolValue;
        }
        else if (sourceValue is IEnumerable<int> ||
            sourceValue is IEnumerable<long> ||
            sourceValue is IEnumerable<string> ||
            sourceValue is IEnumerable<float> ||
            sourceValue is IEnumerable<double> ||
            sourceValue is IEnumerable<bool>)
        {
            var listValue = sourceValue as IEnumerable<object>;
            value.ListValue = new ListValue();
            foreach (var item in listValue!)
            {
                value.ListValue.Values.Add(ConvertToGrpcFieldValue(item));
            }
        }
        else
        {
            throw new InvalidOperationException($"Unsupported source value type {sourceValue?.GetType().FullName}.");
        }

        return value;
    }
}
