// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Memory;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Microsoft.SemanticKernel.Connectors.Qdrant;

/// <summary>
/// Vector store that uses Qdrant as the underlying storage.
/// </summary>
/// <typeparam name="TDataModel">The data model to use for adding, updating and retrieving data from storage.</typeparam>
public class QdrantVectorStore<TDataModel> : IVectorStore<TDataModel>
    where TDataModel : class, new()
{
    /// <summary>A set of types that fields on the provided model may have.</summary>
    private static readonly HashSet<Type> s_supportedFieldTypes = new() { typeof(string), typeof(Int32), typeof(Int64), typeof(double), typeof(bool), typeof(Int32?), typeof(Int64?), typeof(double?), typeof(bool?) };

    /// <summary>Qdrant client that can be used to manage the points in a Qdrant store.</summary>
    private readonly QdrantClient _qdrantClient;

    /// <summary>The name of the collection to use with this store if none is provided for any individual operation.</summary>
    private readonly string _defaultCollectionName;

    /// <summary>Optional configuration options for this class.</summary>
    private readonly QdrantVectorStoreOptions _options;

    /// <summary>A list of property info objects that point at the payload fields in the current model, and allows easy reading and writing of these properties.</summary>
    private readonly List<PropertyInfo> _payloadFieldsPropertyInfo = new();

    /// <summary>A list of property info objects that point at the vector fields in the current model, and allows easy reading and writing of these properties.</summary>
    private readonly List<PropertyInfo> _vectorFieldsPropertyInfo = new();

    /// <summary>A property info object that points at the key field for the current model, allowing easy reading and writing of this property.</summary>
    private readonly PropertyInfo _keyFieldPropertyInfo;

    /// <summary>
    /// Initializes a new instance of the <see cref="QdrantVectorStore{TDataModel}"/> class.
    /// </summary>
    /// <param name="qdrantClient">Qdrant client that can be used to manage the points in a Qdrant store.</param>
    /// <param name="defaultCollectionName">The name of the collection to use with this store if none is provided for any individual operation.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public QdrantVectorStore(QdrantClient qdrantClient, string defaultCollectionName, QdrantVectorStoreOptions? options)
    {
        this._qdrantClient = qdrantClient ?? throw new ArgumentNullException(nameof(qdrantClient));
        this._defaultCollectionName = string.IsNullOrWhiteSpace(defaultCollectionName) ? throw new ArgumentException("Default collection name is required.", nameof(defaultCollectionName)) : defaultCollectionName;
        this._options = options ?? new QdrantVectorStoreOptions();

        var fields = VectorStoreModelPropertyReader.FindFields(typeof(TDataModel), this._options.HasNamedVectors);
        VectorStoreModelPropertyReader.VerifyFieldTypes(fields.dataFields, s_supportedFieldTypes, "Data");
        VectorStoreModelPropertyReader.VerifyFieldTypes(fields.dataFields, s_supportedFieldTypes, "Metadata");

        this._keyFieldPropertyInfo = fields.keyField;
        this._payloadFieldsPropertyInfo = fields.dataFields.Concat(fields.metadataFields).ToList();
        this._vectorFieldsPropertyInfo = fields.vectorFields;
    }

    /// <inheritdoc />
    public async Task<TDataModel?> GetAsync(string key, VectorStoreGetDocumentOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        // Create options.
        var collectionName = options?.CollectionName ?? this._defaultCollectionName;
        (var pointId, _) = ParseKey(this._options.IdType, key);

        // Retrieve data points.
        var retrievedPoints = await this._qdrantClient.RetrieveAsync(collectionName, [pointId], true, options?.IncludeEmbeddings ?? false, cancellationToken: cancellationToken).ConfigureAwait(false);

        // Check that we found something.
        if (retrievedPoints.Count == 0)
        {
            throw new HttpOperationException(HttpStatusCode.NotFound, null, null, null);
        }

        // Map the retrieved point to the target data model.
        var retrievedPoint = retrievedPoints[0];
        var target = new TDataModel();

        ////JsonFormatter formatter = new(new JsonFormatter.Settings(false));
        ////string json = formatter.Format(retrievedPoint);

        // First set the key field.
        this._keyFieldPropertyInfo.SetValue(target, retrievedPoint.Id.Num.ToString(CultureInfo.InvariantCulture));

        // Map each embedding (vector) field.
        if (options?.IncludeEmbeddings is true)
        {
            foreach (var vectorFieldPropertyInfo in this._vectorFieldsPropertyInfo)
            {
                var propertyName = VectorStoreModelPropertyReader.GetSerializedPropertyName(vectorFieldPropertyInfo);

                if (this._options.HasNamedVectors)
                {
                    if (retrievedPoint.Vectors.Vectors_.Vectors.TryGetValue(propertyName, out Vector vectorValue))
                    {
                        vectorFieldPropertyInfo.SetValue(target, new ReadOnlyMemory<float>(vectorValue.Data.ToArray()));
                    }
                }
                else
                {
                    vectorFieldPropertyInfo.SetValue(target, new ReadOnlyMemory<float>(retrievedPoint.Vectors.Vector.Data.ToArray()));
                }
            }
        }

        // Map each payload field.
        foreach (var payloadFieldPropertyInfo in this._payloadFieldsPropertyInfo)
        {
            var propertyName = VectorStoreModelPropertyReader.GetSerializedPropertyName(payloadFieldPropertyInfo);

            if (retrievedPoint.Payload.TryGetValue(propertyName, out Value payloadValue))
            {
                payloadFieldPropertyInfo.SetValue(target, ConvertFromGrpcFieldValue(payloadValue, payloadFieldPropertyInfo.PropertyType));
            }
        }

        return target;
    }

    /// <inheritdoc />
    public async Task<string> RemoveAsync(string key, VectorStoreRemoveDocumentOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        // Create options.
        var collectionName = options?.CollectionName ?? this._defaultCollectionName;
        (var pointId, var guid) = ParseKey(this._options.IdType, key);

        // Delete the data point using GUID.
        if (pointId.HasUuid)
        {
            await this._qdrantClient.DeleteAsync(collectionName, guid!.Value, wait: true, cancellationToken: cancellationToken).ConfigureAwait(false);
            return key;
        }

        // Delete the data point using ulong.
        await this._qdrantClient.DeleteAsync(collectionName, pointId.Num, wait: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        return key;
    }

    /// <inheritdoc />
    public async Task<string> UpsertAsync(TDataModel record, VectorStoreUpsertDocumentOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (record is null)
        {
            throw new ArgumentNullException(nameof(record));
        }

        // Create options.
        var collectionName = options?.CollectionName ?? this._defaultCollectionName;
        var key = this._keyFieldPropertyInfo.GetValue(record)?.ToString() ?? throw new ArgumentException($"Missing key field {this._keyFieldPropertyInfo.Name} on provided record of type {typeof(TDataModel).FullName}.", nameof(record));
        (var pointId, _) = ParseKey(this._options.IdType, key);

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
            var propertyValue = payloadFieldPropertyInfo.GetValue(record);
            pointStruct.Payload.Add(propertyName, ConvertToGrpcFieldValue(propertyValue));
        }

        // Add vectors.
        if (this._options.HasNamedVectors)
        {
            var namedVectors = new NamedVectors();
            foreach (var vectorFieldPropertyInfo in this._vectorFieldsPropertyInfo)
            {
                var propertyName = VectorStoreModelPropertyReader.GetSerializedPropertyName(vectorFieldPropertyInfo);
                var propertyValue = vectorFieldPropertyInfo.GetValue(record);
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
            var propertyValue = (ReadOnlyMemory<float>)vectorFieldPropertyInfo.GetValue(record);
            pointStruct.Vectors.Vector = propertyValue.ToArray();
        }

        // Upsert.
        await this._qdrantClient.UpsertAsync(collectionName, [pointStruct], true, cancellationToken: cancellationToken).ConfigureAwait(false);
        return key;
    }

    /// <summary>
    /// Parse the given key based on the provided ID type and return a <see cref="PointId"/> containing the key information, plus an optional <see cref="Guid"/>.
    /// </summary>
    /// <param name="idType">The type of id to parse the string to.</param>
    /// <param name="key">The key to parse.</param>
    /// <exception cref="ArgumentException">Thrown if the id type could not be parsed correctly.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the id type is unknown.</exception>
    /// <returns>A point id and optionally a guid from the given key.</returns>
    private static (PointId pointId, Guid? guid) ParseKey(QdrantVectorStoreOptions.QdrantIdType idType, string key)
    {
        var pointId = new PointId();
        var guid = default(Guid);
        switch (idType)
        {
            case QdrantVectorStoreOptions.QdrantIdType.UUID:
                pointId.Uuid = key;
                try
                {
                    guid = Guid.Parse(key);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException("Key must be a valid guid when using UUID ID type.", nameof(key), ex);
                }
                break;

            case QdrantVectorStoreOptions.QdrantIdType.Ulong:
                try
                {
                    var parsedULongKey = ulong.Parse(key, CultureInfo.InvariantCulture);
                    pointId.Num = parsedULongKey;
                }
                catch (FormatException ex)
                {
                    throw new ArgumentException("Key must be a valid ulong when using Ulong ID type.", nameof(key), ex);
                }
                catch (OverflowException ex)
                {
                    throw new ArgumentException("Key must be a valid ulong when using Ulong ID type.", nameof(key), ex);
                }
                break;

            default:
                throw new InvalidOperationException($"Unknown ID type: {idType}");
        }

        return (pointId, guid);
    }

    /// <summary>
    /// Convert the given <paramref name="payloadValue"/> to the correct native type based on its properties.
    /// </summary>
    /// <param name="payloadValue">The value to convert to a native type.</param>
    /// <param name="targetType">The target type that we will need to consume.</param>
    /// <returns>The converted native value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when an unsupported type is enountered.</exception>
    private static object? ConvertFromGrpcFieldValue(Value payloadValue, Type targetType)
    {
        return payloadValue.KindCase switch
        {
            Value.KindOneofCase.NullValue => null,
            // Cast to object to avoid both values being implicitly cast back to Int64 again.
            Value.KindOneofCase.IntegerValue => targetType == typeof(int) || targetType == typeof(int?) ? (object)(int)payloadValue.IntegerValue : (object)payloadValue.IntegerValue,
            Value.KindOneofCase.StringValue => payloadValue.StringValue,
            Value.KindOneofCase.DoubleValue => payloadValue.DoubleValue,
            Value.KindOneofCase.BoolValue => payloadValue.BoolValue,
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
        else if (sourceValue is string stringValue)
        {
            value.StringValue = stringValue;
        }
        else if (sourceValue is double doubleValue)
        {
            value.DoubleValue = doubleValue;
        }
        else if (sourceValue is bool boolValue)
        {
            value.BoolValue = boolValue;
        }
        else
        {
            throw new InvalidOperationException($"Unsupported source value type {sourceValue?.GetType().FullName}.");
        }

        return value;
    }
}
