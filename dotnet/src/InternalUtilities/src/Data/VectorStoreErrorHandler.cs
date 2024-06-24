﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.SemanticKernel.Memory;

namespace Microsoft.SemanticKernel;

/// <summary>
/// Contains helpers for reading vector store model properties and their attributes.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class VectorStoreErrorHandler
{
    /// <summary>
    /// Run the given model conversion and wrap any exceptions with <see cref="VectorStoreRecordMappingException"/>.
    /// </summary>
    /// <typeparam name="T">The response type of the operation.</typeparam>
    /// <param name="databaseSystemName">The name of the database system the operation is being run on.</param>
    /// <param name="collectionName">The name of the collection the operation is being run on.</param>
    /// <param name="operationName">The type of database operation being run.</param>
    /// <param name="operation">The operation to run.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T RunModelConversion<T>(string databaseSystemName, string collectionName, string operationName, Func<T> operation)
    {
        try
        {
            return operation.Invoke();
        }
        catch (Exception ex)
        {
            var wrapperException = new VectorStoreRecordMappingException("Failed to convert vector store record.", ex);

            // Using Open Telemetry standard for naming of these entries.
            // https://opentelemetry.io/docs/specs/semconv/attributes-registry/db/
            wrapperException.Data.Add("db.system", databaseSystemName);
            wrapperException.Data.Add("db.collection.name", collectionName);
            wrapperException.Data.Add("db.operation.name", operationName);

            throw wrapperException;
        }
    }
}
