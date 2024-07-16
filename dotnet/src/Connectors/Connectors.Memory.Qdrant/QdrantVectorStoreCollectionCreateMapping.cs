// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using Microsoft.SemanticKernel.Data;
using Qdrant.Client.Grpc;

namespace Microsoft.SemanticKernel.Connectors.Qdrant;

/// <summary>
/// Contains mapping helpers to use when creating a qdrant vector collection.
/// </summary>
internal static class QdrantVectorStoreCollectionCreateMapping
{
    /// <summary>A dictionary of types and their matching qdrant index schema type.</summary>
    public static readonly Dictionary<Type, PayloadSchemaType> s_schemaTypeMap = new()
    {
        { typeof(short), PayloadSchemaType.Integer },
        { typeof(sbyte), PayloadSchemaType.Integer },
        { typeof(byte), PayloadSchemaType.Integer },
        { typeof(ushort), PayloadSchemaType.Integer },
        { typeof(int), PayloadSchemaType.Integer },
        { typeof(uint), PayloadSchemaType.Integer },
        { typeof(long), PayloadSchemaType.Integer },
        { typeof(ulong), PayloadSchemaType.Integer },
        { typeof(float), PayloadSchemaType.Float },
        { typeof(double), PayloadSchemaType.Float },
        { typeof(decimal), PayloadSchemaType.Float },

        { typeof(short?), PayloadSchemaType.Integer },
        { typeof(sbyte?), PayloadSchemaType.Integer },
        { typeof(byte?), PayloadSchemaType.Integer },
        { typeof(ushort?), PayloadSchemaType.Integer },
        { typeof(int?), PayloadSchemaType.Integer },
        { typeof(uint?), PayloadSchemaType.Integer },
        { typeof(long?), PayloadSchemaType.Integer },
        { typeof(ulong?), PayloadSchemaType.Integer },
        { typeof(float?), PayloadSchemaType.Float },
        { typeof(double?), PayloadSchemaType.Float },
        { typeof(decimal?), PayloadSchemaType.Float },

        { typeof(string), PayloadSchemaType.Text },
        { typeof(DateTime), PayloadSchemaType.Datetime },
        { typeof(bool), PayloadSchemaType.Bool },

        { typeof(DateTime?), PayloadSchemaType.Datetime },
        { typeof(bool?), PayloadSchemaType.Bool },
    };

    /// <summary>
    /// Get the configured <see cref="Distance"/> from the given <paramref name="vectorProperty"/>.
    /// If none is configured, the default is <see cref="Distance.Cosine"/>.
    /// </summary>
    /// <param name="vectorProperty">The vector property definition.</param>
    /// <returns>The chosen <see cref="Distance"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if a distance function is chosen that isn't supported by Azure AI Search.</exception>
    public static Distance GetSDKDistanceAlgorithm(VectorStoreRecordVectorProperty vectorProperty)
    {
        if (vectorProperty.DistanceFunction is null)
        {
            return Distance.Cosine;
        }

        return vectorProperty.DistanceFunction switch
        {
            DistanceFunction.CosineSimilarity => Distance.Cosine,
            DistanceFunction.DotProductSimilarity => Distance.Dot,
            DistanceFunction.EuclideanDistance => Distance.Euclid,
            DistanceFunction.ManhattanDistance => Distance.Manhattan,
            _ => throw new InvalidOperationException($"Unsupported distance function '{vectorProperty.DistanceFunction}' for {nameof(VectorStoreRecordVectorProperty)} '{vectorProperty.PropertyName}'.")
        };
    }
}
