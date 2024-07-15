﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// Service for storing and retrieving vector records, that uses an in memory dictionary as the underlying storage.
/// </summary>
/// <typeparam name="TRecord">The data model to use for adding, updating and retrieving data from storage.</typeparam>
[Experimental("SKEXP0001")]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public sealed class VolatileVectorStoreRecordCollection<TRecord> : IVectorStoreRecordCollection<string, TRecord>
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
    where TRecord : class
{
    /// <summary>Internal storage for the record collection.</summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, TRecord>> _internalCollection;

    /// <summary>Optional configuration options for this class.</summary>
    private readonly VolatileVectorStoreRecordCollectionOptions _options;

    /// <summary>The name of the collection that this <see cref="VolatileVectorStoreRecordCollection{TRecord}"/> will access.</summary>
    private readonly string _collectionName;

    /// <summary>A set of types that a key on the provided model may have.</summary>
    private static readonly HashSet<Type> s_supportedKeyTypes =
    [
        typeof(string)
    ];

    /// <summary>A property info object that points at the key property for the current model, allowing easy reading and writing of this property.</summary>
    private readonly PropertyInfo _keyPropertyInfo;

    /// <summary>
    /// Initializes a new instance of the <see cref="VolatileVectorStoreRecordCollection{TRecord}"/> class.
    /// </summary>
    /// <param name="collectionName">The name of the collection that this <see cref="VolatileVectorStoreRecordCollection{TRecord}"/> will access.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    public VolatileVectorStoreRecordCollection(string collectionName, VolatileVectorStoreRecordCollectionOptions? options = default)
    {
        // Verify.
        Verify.NotNullOrWhiteSpace(collectionName);

        // Assign.
        this._collectionName = collectionName;
        this._internalCollection = new();
        this._options = options ?? new VolatileVectorStoreRecordCollectionOptions();

        // Enumerate public properties using configuration or attributes.
        (PropertyInfo keyProperty, List<PropertyInfo> dataProperties, List<PropertyInfo> vectorProperties) properties;
        if (this._options.VectorStoreRecordDefinition is not null)
        {
            properties = VectorStoreRecordPropertyReader.FindProperties(typeof(TRecord), this._options.VectorStoreRecordDefinition, supportsMultipleVectors: true);
        }
        else
        {
            properties = VectorStoreRecordPropertyReader.FindProperties(typeof(TRecord), supportsMultipleVectors: true);
        }

        // Validate property types and store for later use.
        VectorStoreRecordPropertyReader.VerifyPropertyTypes([properties.keyProperty], s_supportedKeyTypes, "Key");
        this._keyPropertyInfo = properties.keyProperty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VolatileVectorStoreRecordCollection{TRecord}"/> class.
    /// </summary>
    /// <param name="internalCollection">Allows passing in the dictionary used for storage, for testing purposes.</param>
    /// <param name="collectionName">The name of the collection that this <see cref="VolatileVectorStoreRecordCollection{TRecord}"/> will access.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    internal VolatileVectorStoreRecordCollection(ConcurrentDictionary<string, ConcurrentDictionary<string, TRecord>> internalCollection, string collectionName, VolatileVectorStoreRecordCollectionOptions? options = default)
        : this(collectionName, options)
    {
        this._internalCollection = internalCollection;
    }

    /// <inheritdoc />
    public string CollectionName => this._collectionName;

    /// <inheritdoc />
    public Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        return this._internalCollection.ContainsKey(this._collectionName) ? Task.FromResult(true) : Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task DeleteCollectionAsync(CancellationToken cancellationToken = default)
    {
        this._internalCollection.TryRemove(this._collectionName, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<TRecord?> GetAsync(string key, GetRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        var collectionDictionary = this.GetCollectionDictionary();

        if (collectionDictionary.TryGetValue(key, out var record))
        {
            return Task.FromResult<TRecord?>(record);
        }

        return Task.FromResult<TRecord?>(null);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TRecord> GetBatchAsync(IEnumerable<string> keys, GetRecordOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
        {
            var record = await this.GetAsync(key, options, cancellationToken).ConfigureAwait(false);

            if (record is not null)
            {
                yield return record;
            }
        }
    }

    /// <inheritdoc />
    public Task DeleteAsync(string key, DeleteRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        var collectionDictionary = this.GetCollectionDictionary();

        collectionDictionary.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteBatchAsync(IEnumerable<string> keys, DeleteRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        var collectionDictionary = this.GetCollectionDictionary();

        foreach (var key in keys)
        {
            collectionDictionary.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string> UpsertAsync(TRecord record, UpsertRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        var collectionDictionary = this.GetCollectionDictionary();

        var key = this._keyPropertyInfo.GetValue(record) as string;
        collectionDictionary.AddOrUpdate(key!, record, (key, currentValue) => record);

        return Task.FromResult(key!);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> UpsertBatchAsync(IEnumerable<TRecord> records, UpsertRecordOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var record in records)
        {
            yield return await this.UpsertAsync(record, options, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Get the collection dictionary from the internal storage, throws if it does not exist.
    /// </summary>
    /// <returns>The retrieved collection dictionary.</returns>
    private ConcurrentDictionary<string, TRecord> GetCollectionDictionary()
    {
        if (!this._internalCollection.TryGetValue(this._collectionName, out var collectionDictionary))
        {
            throw new VectorStoreOperationException($"Call to vector store failed. Collection '{this._collectionName}' does not exist.");
        }

        return collectionDictionary;
    }
}
