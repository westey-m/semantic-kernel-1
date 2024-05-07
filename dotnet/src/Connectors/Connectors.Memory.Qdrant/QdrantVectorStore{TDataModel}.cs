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
        (this._keyFieldPropertyInfo, this._payloadFieldsPropertyInfo, this._vectorFieldsPropertyInfo) = FindFields(typeof(TDataModel), this._options.HasNamedVectors);
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
        var pointId = new PointId();
        ParseKey(this._options.IdType, key, pointId);

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
                if (this._options.HasNamedVectors)
                {
                    if (retrievedPoint.Vectors.Vectors_.Vectors.TryGetValue(vectorFieldPropertyInfo.Name, out Vector vectorValue))
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
            if (retrievedPoint.Payload.TryGetValue(payloadFieldPropertyInfo.Name, out Value payloadValue))
            {
                payloadFieldPropertyInfo.SetValue(target, ConvertFromGrpcFieldValue(payloadValue, payloadFieldPropertyInfo.PropertyType));
            }
        }

        return target;
    }

    /// <inheritdoc />
    public Task<string> RemoveAsync(string key, VectorStoreRemoveDocumentOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<string> UpsertAsync(TDataModel record, VectorStoreUpsertDocumentOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Parse the given key based on the provided ID type and populate the given <see cref="PointId"/> with the result.
    /// </summary>
    /// <param name="idType">The type of id to parse the string to.</param>
    /// <param name="key">The key to parse.</param>
    /// <param name="pointId">The <see cref="PointId"/> to populate with the parsed value.</param>
    /// <exception cref="ArgumentException">Thrown if the id type could not be parsed correctly.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the id type is unknown.</exception>
    private static void ParseKey(QdrantVectorStoreOptions.QdrantIdType idType, string key, PointId pointId)
    {
        switch (idType)
        {
            case QdrantVectorStoreOptions.QdrantIdType.UUID:
                pointId.Uuid = key;
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
    }

    /// <summary>
    /// Find the fields with <see cref="VectorStoreModelKeyAttribute"/>, <see cref="VectorStoreModelDataAttribute"/>, <see cref="VectorStoreModelMetadataAttribute"/> and <see cref="VectorStoreModelVectorAttribute"/> attributes.
    /// Store those fields in the corresponding lists.
    /// Throws if no key field is found, if there are multiple key fields, or if the key field is not a string.
    /// </summary>
    /// <param name="type">The data model to find the fields on.</param>
    /// <param name="hasNamedVectors">A value indicating whether the qdrant collection that we are to access has named vectors or a single unnamed vector.</param>
    /// <returns>The categorized fields.</returns>
    private static (PropertyInfo keyField, List<PropertyInfo> payloadFields, List<PropertyInfo> vectorFields) FindFields(Type type, bool hasNamedVectors)
    {
        PropertyInfo? keyField = null;
        List<PropertyInfo> payloadFields = new();
        List<PropertyInfo> vectorFields = new();
        bool singleVectorPropertyFound = false;

        foreach (var property in type.GetProperties())
        {
            // Get Key property.
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

            // Since Data and Metadata are mapped to the same dictionary in the Qdrant client, we can store them in the same list.
            if (property.GetCustomAttribute<VectorStoreModelDataAttribute>() is not null || property.GetCustomAttribute<VectorStoreModelMetadataAttribute>() is not null)
            {
                if (!s_supportedFieldTypes.Contains(property.PropertyType))
                {
                    throw new ArgumentException($"Data and metadata fields must be one of the supportd types (bool, string, Int32, Int64, double). Type of {property.Name} is {property.PropertyType.FullName}.");
                }

                payloadFields.Add(property);
            }

            // Get Vector properties.
            if (property.GetCustomAttribute<VectorStoreModelVectorAttribute>() is not null)
            {
                if (property.PropertyType != typeof(ReadOnlyMemory<float>) && property.PropertyType != typeof(ReadOnlyMemory<float>?))
                {
                    throw new ArgumentException($"Vector fields must be of type ReadOnlyMemory<float> or ReadOnlyMemory<float>?. Type of {property.Name} is {property.PropertyType.FullName}.");
                }

                // If we have named vectors, we can have many vector fields.
                if (hasNamedVectors)
                {
                    vectorFields.Add(property);
                }
                // If we don't have named vectors, we can only have one vector field.
                else if (!singleVectorPropertyFound)
                {
                    vectorFields.Add(property);
                    singleVectorPropertyFound = true;
                }
                else
                {
                    throw new ArgumentException($"Multiple vector fields found on type {type.FullName} while HasNamedVectors option is set to false.");
                }
            }
        }

        // Check that we have a key field.
        if (keyField is null)
        {
            throw new ArgumentException($"No key field found on type {type.FullName}.");
        }

        // Check that we have one vector field if we don't have named vectors.
        if (!hasNamedVectors && !singleVectorPropertyFound)
        {
            throw new ArgumentException($"No vector field found on type {type.FullName}.");
        }

        return (keyField, payloadFields, vectorFields);
    }

    /// <summary>
    /// Convert the given <paramref name="payloadValue"/> to the correct native type based on its properties.
    /// </summary>
    /// <param name="payloadValue">The value to convert to a native type.</param>
    /// <param name="targetType">The target type that we will need to consume.</param>
    /// <returns>The converted native value.</returns>
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
}
