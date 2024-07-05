// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Embeddings;

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// Decorator for a <see cref="IVectorRecordStore{TKey, TRecord}"/> that generates embeddings for records on upsert.
/// </summary>
/// <typeparam name="TKey">The data type of the record key.</typeparam>
/// <typeparam name="TRecord">The record data model to use for adding, updating and retrieving data from the store.</typeparam>
[Experimental("SKEXP0001")]
public class TextEmbeddingVectorRecordStore<TKey, TRecord> : IVectorRecordStore<TKey, TRecord>
    where TRecord : class
{
    /// <summary>The decorated <see cref="IVectorRecordStore{TKey, TRecord}"/>.</summary>
    private readonly IVectorRecordStore<TKey, TRecord> _decoratedVectorRecordStore;

    /// <summary>The service to use for generating the embeddings.</summary>
    private readonly ITextEmbeddingGenerationService _textEmbeddingGenerationService;

    /// <summary>Optional configuration options for this class.</summary>
    private readonly TextEmbeddingVectorRecordStoreOptions _options;

    /// <summary>Optional configuration options for this class.</summary>
    private readonly IEnumerable<(PropertyInfo PropertyInfo, PropertyInfo EmbeddingPropertyInfo)> _dataPropertiesWithEmbeddingProperties;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextEmbeddingVectorRecordStore{TKey, TRecord}"/> class.
    /// </summary>
    /// <param name="decoratedVectorRecordStore">The decorated <see cref="IVectorRecordStore{TKey, TRecord}"/>.</param>
    /// <param name="textEmbeddingGenerationService">The service to use for generating the embeddings.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    /// <exception cref="ArgumentException">Thrown when data properties are referencing embedding properties that do not exist.</exception>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
    public TextEmbeddingVectorRecordStore(IVectorRecordStore<TKey, TRecord> decoratedVectorRecordStore, ITextEmbeddingGenerationService textEmbeddingGenerationService, TextEmbeddingVectorRecordStoreOptions? options)
    {
        // Verify.
        Verify.NotNull(decoratedVectorRecordStore);
        Verify.NotNull(textEmbeddingGenerationService);

        // Assign.
        this._decoratedVectorRecordStore = decoratedVectorRecordStore;
        this._textEmbeddingGenerationService = textEmbeddingGenerationService;
        this._options = options ?? new TextEmbeddingVectorRecordStoreOptions();

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

        // Find all the data properties to generate embeddings for.
        var vectorPropertiesDictionary = properties.vectorProperties.ToDictionary(p => p.Name);
        this._dataPropertiesWithEmbeddingProperties = FindDataPropertiesWithEmbeddingProperties(properties.dataProperties, vectorPropertiesDictionary);
    }

    /// <inheritdoc />
    public Task DeleteAsync(TKey key, DeleteRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        return this._decoratedVectorRecordStore.DeleteAsync(key, options, cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteBatchAsync(IEnumerable<TKey> keys, DeleteRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        return this._decoratedVectorRecordStore.DeleteBatchAsync(keys, options, cancellationToken);
    }

    /// <inheritdoc />
    public Task<TRecord?> GetAsync(TKey key, GetRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        return this._decoratedVectorRecordStore.GetAsync(key, options, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TRecord> GetBatchAsync(IEnumerable<TKey> keys, GetRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        return this._decoratedVectorRecordStore.GetBatchAsync(keys, options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TKey> UpsertAsync(TRecord record, UpsertRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        var recordWithEmbeddings = await this.AddEmbeddingsAsync(record, cancellationToken).ConfigureAwait(false);
        return await this._decoratedVectorRecordStore.UpsertAsync(recordWithEmbeddings, options, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TKey> UpsertBatchAsync(IEnumerable<TRecord> records, UpsertRecordOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var recordWithEmbeddingsTasks = records.Select(r => this.AddEmbeddingsAsync(r, cancellationToken));
        var recordWithEmbeddings = await Task.WhenAll(recordWithEmbeddingsTasks).ConfigureAwait(false);
        var upserResults = this._decoratedVectorRecordStore.UpsertBatchAsync(records, options, cancellationToken);
        await foreach (var upsertResult in upserResults.ConfigureAwait(false))
        {
            yield return upsertResult;
        }
    }

    /// <summary>
    /// Generate and add embeddings for each data field that has an embedding property on the provided record.
    /// </summary>
    /// <param name="record">The record to generate embeddings for.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>The record with embeddings added.</returns>
    private async Task<TRecord> AddEmbeddingsAsync(TRecord record, CancellationToken cancellationToken)
    {
        foreach (var (dataPropertyInfo, embeddingPropertyInfo) in this._dataPropertiesWithEmbeddingProperties)
        {
            var dataValue = dataPropertyInfo.GetValue(record);
            if (dataValue is string dataString)
            {
                var embeddingValue = await this._textEmbeddingGenerationService.GenerateEmbeddingAsync(dataString, cancellationToken: cancellationToken).ConfigureAwait(false);
                embeddingPropertyInfo.SetValue(record, embeddingValue);
            }
        }

        return record;
    }

    /// <summary>
    /// Get the list of data properties that are referencing embedding properties, and find the related embedding properties.
    /// </summary>
    /// <param name="dataProperties">All data properties on the record.</param>
    /// <param name="vectorProperties">All vector properties on the record.</param>
    /// <returns>The list of data properties that are referencing embedding properties, with their related embedding properties.</returns>
    private static IEnumerable<(PropertyInfo PropertyInfo, PropertyInfo EmbeddingPropertyInfo)> FindDataPropertiesWithEmbeddingProperties(List<PropertyInfo> dataProperties, Dictionary<string, PropertyInfo> vectorProperties)
    {
        foreach (var property in dataProperties)
        {
            var attribute = property.GetCustomAttribute<VectorStoreRecordDataAttribute>();
            if (attribute is not null && attribute.HasEmbedding && !string.IsNullOrWhiteSpace(attribute.EmbeddingPropertyName))
            {
                if (vectorProperties.TryGetValue(attribute.EmbeddingPropertyName!, out var embeddingpropertyInfo))
                {
                    yield return (property, embeddingpropertyInfo);
                }
                else
                {
                    throw new ArgumentException($"The embedding property '{attribute.EmbeddingPropertyName}' as referenced by data property '{property.Name}' does not exist in the record model.");
                }
            }
        }
    }
}
