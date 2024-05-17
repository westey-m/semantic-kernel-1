// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.SemanticKernel.Memory;
using Qdrant.Client.Grpc;

namespace Microsoft.SemanticKernel.Connectors.Qdrant;

/// <summary>
/// Mapper between a Qdrant record and <see cref="VectorDBRecord"/>.
/// </summary>
public class QdrantVectorDBRecordMapper : IQdrantVectorDBRecordMapper<VectorDBRecord>
{
    /// <summary>Optional options to use when doing the model conversion.</summary>
    private readonly QdrantVectorDBRecordMapperOptions _options;

    /// <summary>The names of fields that contain the string fragments that are used to create embeddings.</summary>
    private readonly HashSet<string> _stringDataFieldNames;

    /// <summary>The names of fields that contain additional data. This can be any data that the embedding is not based on.</summary>
    private readonly HashSet<string> _metadataFieldNames;

    /// <summary>
    /// Initializes a new instance of the <see cref="QdrantVectorDBRecordMapper"/> class.
    /// </summary>
    /// <param name="options">Optional options to use when doing the model conversion.</param>
    public QdrantVectorDBRecordMapper(QdrantVectorDBRecordMapperOptions options)
    {
        this._options = options ?? new QdrantVectorDBRecordMapperOptions();
        this._stringDataFieldNames = new HashSet<string>(this._options.StringDataFieldNames);
        this._metadataFieldNames = new HashSet<string>(this._options.MetadataFieldNames);
    }

    /// <inheritdoc />
    public PointStruct ConvertFromDataModelToGrpc(VectorDBRecord record)
    {
        // Create point id.
        PointId pointId;
        if (record.Key is ulong numberKey)
        {
            pointId = new PointId { Num = numberKey };
        }
        else if (record.Key is Guid guidKey)
        {
            pointId = new PointId { Uuid = guidKey.ToString("D") };
        }
        else
        {
            throw new InvalidOperationException($"Unsupported key type {record.Key.GetType().FullName}.");
        }

        // Create point.
        var pointStruct = new PointStruct
        {
            Id = pointId,
            Vectors = new Vectors(),
            Payload = { },
        };

        // Add data fields.
        foreach (var item in record.StringData)
        {
            if (item.Value is null)
            {
                pointStruct.Payload.Add(item.Key, null);
            }
            else
            {
                pointStruct.Payload.Add(item.Key, item.Value);
            }
        }

        // Add metadata fields.
        foreach (var item in record.Metadata)
        {
            pointStruct.Payload.Add(item.Key, ConvertToGrpcFieldValue(item.Value));
        }

        if (record.Vectors.Count > 0)
        {
            // Add named vector fields.
            if (this._options.HasNamedVectors)
            {
                var namedVectors = new NamedVectors();
                foreach (var item in record.Vectors)
                {
                    if (item.Value is null)
                    {
                        namedVectors.Vectors.Add(item.Key, null);
                    }
                    else
                    {
                        var floatArray = ConvertToFloatArray((ReadOnlyMemory<object>)item.Value);
                        namedVectors.Vectors.Add(item.Key, floatArray);
                    }
                }

                pointStruct.Vectors.Vectors_ = namedVectors;
            }
            else
            {
                // Add single vector field.
                if (record.Vectors.Count != 1)
                {
                    throw new InvalidOperationException($"When not using named vectors, a single vector entry is expected in the NamedVectors dictionary, with an empty string key. Found {record.Vectors.Count} entries.");
                }

                if (!record.Vectors.TryGetValue(string.Empty, out var vectorField))
                {
                    throw new InvalidOperationException("When not using named vectors, a single vector entry is expected in the NamedVectors dictionary, with an empty string key. Found no entry with an empty string key.");
                }

                if (vectorField is not null)
                {
                    var floatArray = ConvertToFloatArray((ReadOnlyMemory<object>)vectorField);
                    pointStruct.Vectors.Vector = (float[])floatArray;
                }
            }
        }

        return pointStruct;
    }

    /// <inheritdoc />
    public VectorDBRecord ConvertFromGrpcToDataModel(RetrievedPoint point, GetRecordOptions? options = null)
    {
        // Create record key based type of point id.
        object key;
        if (point.Id.HasNum)
        {
            key = point.Id.Num;
        }
        else if (point.Id.HasUuid)
        {
            key = Guid.Parse(point.Id.Uuid);
        }
        else
        {
            throw new InvalidOperationException("Point id must have either a Num or Uuid value.");
        }

        // Convert string data and metadata fields.
        var stringData = new Dictionary<string, string?>();
        var metadata = new Dictionary<string, object?>();
        foreach (var item in point.Payload)
        {
            // Convert the string data fields.
            if (this._stringDataFieldNames.Contains(item.Key))
            {
                if (item.Value is null)
                {
                    stringData.Add(item.Key, null);
                }
                else if (item.Value.HasStringValue)
                {
                    stringData.Add(item.Key, item.Value.StringValue);
                }
                else
                {
                    throw new InvalidOperationException($"Data fields must be of type string. Field '{item.Key}' is of type '{item.Value.KindCase}'.");
                }
            }

            // Convert the metadata fields.
            if (this._metadataFieldNames.Contains(item.Key))
            {
                if (item.Value is null)
                {
                    metadata.Add(item.Key, null);
                }
                else
                {
                    metadata.Add(item.Key, ConvertFromGrpcFieldValue(item.Value));
                }
            }
        }

        // Convert the vector fields if needed.
        var vectors = new Dictionary<string, ReadOnlyMemory<object>?>();
        if (options?.IncludeVectors is true)
        {
            if (this._options.HasNamedVectors)
            {
                // Convert named vectors.
                foreach (var item in point.Vectors.Vectors_.Vectors)
                {
                    var objectArray = Array.CreateInstance(typeof(object), item.Value.Data.Count);
                    Array.Copy(item.Value.Data.ToArray(), objectArray, item.Value.Data.Count);
                    vectors.Add(item.Key, new ReadOnlyMemory<object>((object[])objectArray));
                }
            }
            else if (point.Vectors is not null)
            {
                // Convert single vector.
                var objectArray = Array.CreateInstance(typeof(object), point.Vectors.Vector.Data.Count);
                Array.Copy(point.Vectors.Vector.Data.ToArray(), objectArray, point.Vectors.Vector.Data.Count);
                vectors.Add(string.Empty, new ReadOnlyMemory<object>((object[])objectArray));
            }
        }

        // Create the record.
        var record = new VectorDBRecord(key)
        {
            StringData = stringData,
            Metadata = metadata,
            Vectors = vectors,
        };

        return record;
    }

    /// <summary>
    /// Check that the given <paramref name="vector"/> is the correct type and if so convert it
    /// to a float array.
    /// </summary>
    /// <param name="vector">The vector to convert.</param>
    /// <returns>The converted float array.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the provided vector is not an array of floats.</exception>
    private static float[] ConvertToFloatArray(ReadOnlyMemory<object> vector)
    {
        var vectorArray = vector.ToArray();
        var vectorTypes = vectorArray.Select(value => value.GetType()).Distinct().ToList();

        if (vectorTypes.Count > 1)
        {
            throw new InvalidOperationException("Vector field values must all be of the same type.");
        }

        var vectorType = vectorTypes[0];

        if (vectorType == typeof(float))
        {
            // Convert to float array.
            var floatArray = Array.CreateInstance(typeof(float), vectorArray.Length);
            Array.Copy(vectorArray, floatArray, vectorArray.Length);
            return (float[])floatArray;
        }

        throw new InvalidOperationException("Vector field values must be of type float.");
    }

    /// <summary>
    /// Convert the given <paramref name="payloadValue"/> to the correct native type based on its properties.
    /// </summary>
    /// <param name="payloadValue">The value to convert to a native type.</param>
    /// <returns>The converted native value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when an unsupported type is enountered.</exception>
    private static object? ConvertFromGrpcFieldValue(Value payloadValue)
    {
        return payloadValue.KindCase switch
        {
            Value.KindOneofCase.NullValue => null,
            Value.KindOneofCase.IntegerValue => payloadValue.IntegerValue,
            Value.KindOneofCase.DoubleValue => payloadValue.DoubleValue,
            Value.KindOneofCase.StringValue => payloadValue.StringValue,
            Value.KindOneofCase.BoolValue => payloadValue.BoolValue,
            Value.KindOneofCase.ListValue => payloadValue.ListValue.Values.Select(x => ConvertFromGrpcFieldValue(x)).ToArray(),
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
