// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using JsonSchemaMapper;
using Microsoft.SemanticKernel.Memory;

namespace Microsoft.SemanticKernel;

/// <summary>
/// Contains helpers for reading memory service model properties and their attributes.
/// </summary>
internal static class MemoryServiceModelPropertyReader
{
    /// <summary>
    /// Find the fields with <see cref="KeyAttribute"/>, <see cref="DataAttribute"/> and <see cref="VectorAttribute"/> attributes.
    /// Return those fields in separate categories.
    /// Throws if no key field is found, if there are multiple key fields, or if the key field is not a string.
    /// </summary>
    /// <param name="type">The data model to find the fields on.</param>
    /// <param name="supportsMultipleVectors">A value indicating whether multiple vector fields are supported instead of just one.</param>
    /// <returns>The categorized fields.</returns>
    public static (PropertyInfo keyField, List<PropertyInfo> dataFields, List<PropertyInfo> vectorFields) FindFields(Type type, bool supportsMultipleVectors)
    {
        PropertyInfo? keyField = null;
        List<PropertyInfo> dataProperties = new();
        List<PropertyInfo> metadataProperties = new();
        List<PropertyInfo> vectorFields = new();
        bool singleVectorPropertyFound = false;

        foreach (var property in type.GetProperties())
        {
            // Get Key property.
            if (property.GetCustomAttribute<KeyAttribute>() is not null)
            {
                if (keyField is not null)
                {
                    throw new ArgumentException($"Multiple key fields found on type {type.FullName}.");
                }

                keyField = property;
            }

            // Get data properties.
            if (property.GetCustomAttribute<DataAttribute>() is not null)
            {
                dataProperties.Add(property);
            }

            // Get Vector properties.
            if (property.GetCustomAttribute<VectorAttribute>() is not null)
            {
                if (property.PropertyType != typeof(ReadOnlyMemory<float>) && property.PropertyType != typeof(ReadOnlyMemory<float>?))
                {
                    throw new ArgumentException($"Vector fields must be of type ReadOnlyMemory<float> or ReadOnlyMemory<float>?. Type of {property.Name} is {property.PropertyType.FullName}.");
                }

                // Add all vector fields if we support multiple vectors.
                if (supportsMultipleVectors)
                {
                    vectorFields.Add(property);
                }
                // Add only one vector field if we don't support multiple vectors.
                else if (!singleVectorPropertyFound)
                {
                    vectorFields.Add(property);
                    singleVectorPropertyFound = true;
                }
                else
                {
                    throw new ArgumentException($"Multiple vector fields found on type {type.FullName} while only one is supported.");
                }
            }
        }

        // Check that we have a key field.
        if (keyField is null)
        {
            throw new ArgumentException($"No key field found on type {type.FullName}.");
        }

        // Check that we have one vector field if we don't have named vectors.
        if (!supportsMultipleVectors && !singleVectorPropertyFound)
        {
            throw new ArgumentException($"No vector field found on type {type.FullName}.");
        }

        return (keyField, dataProperties, vectorFields);
    }

    /// <summary>
    /// Verify that the given properties are of the supported types.
    /// </summary>
    /// <param name="properties">The properties to check.</param>
    /// <param name="supportedDataFieldTypes">A set of supported types for data and metadata fields.</param>
    /// <param name="fieldTypeDescription">A description of the types of fields being checked.</param>
    /// <exception cref="ArgumentException">Thrown if any of the properties are not in the given set of types.</exception>
    public static void VerifyFieldTypes(List<PropertyInfo> properties, HashSet<Type> supportedDataFieldTypes, string fieldTypeDescription)
    {
        foreach (var property in properties)
        {
            if (!supportedDataFieldTypes.Contains(property.PropertyType))
            {
                throw new ArgumentException($"{fieldTypeDescription} fields must be one of the supportd types. Type of {property.Name} is {property.PropertyType.FullName}.");
            }
        }
    }

    /// <summary>
    /// Get the serialized name of a property by first checking the <see cref="JsonPropertyNameAttribute"/> and then falling back to the property name.
    /// </summary>
    /// <param name="property">The property to retrieve a serialized name for.</param>
    /// <returns>The serialized name for the property.</returns>
    public static string GetSerializedPropertyName(PropertyInfo property)
    {
        return property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name;
    }
}
