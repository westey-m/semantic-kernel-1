// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.SemanticKernel.Data;

namespace Microsoft.SemanticKernel.Connectors.AzureAISearch;

/// <summary>
/// Contains mapping helpers to use when creating a Azure AI Search vector collection.
/// </summary>
internal static class AzureAISearchVectorStoreCollectionCreateMapping
{
    /// <summary>
    /// Get the configured <see cref="IndexKind"/> from the given <paramref name="vectorProperty"/>.
    /// If none is configured the default is <see cref="IndexKind.Hnsw"/>.
    /// </summary>
    /// <param name="vectorProperty">The vector property definition.</param>
    /// <returns>The chosen <see cref="IndexKind"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if a index type was chosen that isn't supported by Azure AI Search.</exception>
    public static IndexKind GetSKIndexKind(VectorStoreRecordVectorProperty vectorProperty)
    {
        if (vectorProperty.IndexKind is null)
        {
            return IndexKind.Hnsw;
        }

        return vectorProperty.IndexKind switch
        {
            IndexKind.Hnsw => IndexKind.Hnsw,
            IndexKind.Flat => IndexKind.Flat,
            _ => throw new InvalidOperationException($"Unsupported index kind '{vectorProperty.IndexKind}' for {nameof(VectorStoreRecordVectorProperty)} '{vectorProperty.PropertyName}'.")
        };
    }

    /// <summary>
    /// Get the configured <see cref="VectorSearchAlgorithmMetric"/> from the given <paramref name="vectorProperty"/>.
    /// If none is configured, the default is <see cref="VectorSearchAlgorithmMetric.Cosine"/>.
    /// </summary>
    /// <param name="vectorProperty">The vector property definition.</param>
    /// <returns>The chosen <see cref="VectorSearchAlgorithmMetric"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if a distance function is chosen that isn't suported by Azure AI Search.</exception>
    public static VectorSearchAlgorithmMetric GetSDKDistanceAlgorithm(VectorStoreRecordVectorProperty vectorProperty)
    {
        if (vectorProperty.DistanceFunction is null)
        {
            return VectorSearchAlgorithmMetric.Cosine;
        }

        return vectorProperty.DistanceFunction switch
        {
            DistanceFunction.CosineSimilarity => VectorSearchAlgorithmMetric.Cosine,
            DistanceFunction.DotProductSimilarity => VectorSearchAlgorithmMetric.DotProduct,
            DistanceFunction.EuclideanDistance => VectorSearchAlgorithmMetric.Euclidean,
            _ => throw new InvalidOperationException($"Unsupported distance function '{vectorProperty.DistanceFunction}' for {nameof(VectorStoreRecordVectorProperty)} '{vectorProperty.PropertyName}'.")
        };
    }

    /// <summary>
    /// Maps the given property type to the corresponding <see cref="SearchFieldDataType"/>.
    /// </summary>
    /// <param name="propertyType">The property type to map.</param>
    /// <returns>The <see cref="SearchFieldDataType"/> that corresponds to the given property type.</returns>"
    /// <exception cref="InvalidOperationException">Thrown if the given type is not supported.</exception>
    public static SearchFieldDataType GetSDKFieldDataType(Type propertyType)
    {
        return propertyType switch
        {
            Type stringType when stringType == typeof(string) => SearchFieldDataType.String,
            Type boolType when boolType == typeof(bool) || boolType == typeof(bool?) => SearchFieldDataType.Boolean,
            Type intType when intType == typeof(int) || intType == typeof(int?) => SearchFieldDataType.Int32,
            Type longType when longType == typeof(long) || longType == typeof(long?) => SearchFieldDataType.Int64,
            Type floatType when floatType == typeof(float) || floatType == typeof(float?) => SearchFieldDataType.Double,
            Type doubleType when doubleType == typeof(double) || doubleType == typeof(double?) => SearchFieldDataType.Double,
            Type dateTimeType when dateTimeType == typeof(DateTime) || dateTimeType == typeof(DateTime?) => SearchFieldDataType.DateTimeOffset,
            Type dateTimeOffsetType when dateTimeOffsetType == typeof(DateTimeOffset) || dateTimeOffsetType == typeof(DateTimeOffset?) => SearchFieldDataType.DateTimeOffset,
            Type collectionType when typeof(IEnumerable).IsAssignableFrom(collectionType) => SearchFieldDataType.Collection(GetSDKFieldDataType(GetEnumerableType(propertyType))),
            _ => throw new InvalidOperationException($"Unsupported data type '{propertyType}' for {nameof(VectorStoreRecordDataProperty)}.")
        };
    }

    /// <summary>
    /// Gets the type of object stored in the given enumerable type.
    /// </summary>
    /// <param name="type">The enumerable to get the stored type for.</param>
    /// <returns>The type of object stored in the given enumerable type.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the given type is not enumerable.</exception>
    public static Type GetEnumerableType(Type type)
    {
        if (type is IEnumerable)
        {
            return typeof(object);
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            return type.GetGenericArguments()[0];
        }

        var enumerableInterface = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        if (enumerableInterface is not null)
        {
            return enumerableInterface.GetGenericArguments()[0];
        }

        throw new InvalidOperationException($"Unsupported data type '{type}' for {nameof(VectorStoreRecordDataProperty)}.");
    }
}
