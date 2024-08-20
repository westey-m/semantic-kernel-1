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
/// Decorator for a <see cref="IVectorStoreRecordCollection{TKey, TRecord}"/> that generates embeddings for records on upsert.
/// </summary>
/// <typeparam name="TKey">The data type of the record key.</typeparam>
/// <typeparam name="TRecord">The record data model to use for adding, updating and retrieving data from the store.</typeparam>
[Experimental("SKEXP0001")]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public class TextEmbeddingVectorStoreRecordCollection<TKey, TRecord> : IVectorStoreRecordCollection<TKey, TRecord>
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
    where TKey : notnull
    where TRecord : class
{
    /// <summary>The decorated <see cref="IVectorStoreRecordCollection{TKey, TRecord}"/>.</summary>
    private readonly IVectorStoreRecordCollection<TKey, TRecord> _decoratedVectorStoreRecordCollection;

    /// <summary>The service to use for generating the embeddings.</summary>
    private readonly ITextEmbeddingGenerationService _textEmbeddingGenerationService;

    /// <summary>Optional configuration options for this class.</summary>
    private readonly TextEmbeddingVectorStoreRecordCollectionOptions _options;

    /// <summary>Optional configuration options for this class.</summary>
    private readonly IEnumerable<(PropertyInfo PropertyInfo, PropertyInfo EmbeddingPropertyInfo)> _dataPropertiesWithEmbeddingProperties;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextEmbeddingVectorStoreRecordCollection{TKey, TRecord}"/> class.
    /// </summary>
    /// <param name="decoratedVectorStoreRecordCollection">The decorated <see cref="IVectorStoreRecordCollection{TKey, TRecord}"/>.</param>
    /// <param name="textEmbeddingGenerationService">The service to use for generating the embeddings.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    /// <exception cref="ArgumentException">Thrown when data properties are referencing embedding properties that do not exist.</exception>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
    public TextEmbeddingVectorStoreRecordCollection(IVectorStoreRecordCollection<TKey, TRecord> decoratedVectorStoreRecordCollection, ITextEmbeddingGenerationService textEmbeddingGenerationService, TextEmbeddingVectorStoreRecordCollectionOptions? options)
    {
        // Verify.
        Verify.NotNull(decoratedVectorStoreRecordCollection);
        Verify.NotNull(textEmbeddingGenerationService);

        // Assign.
        this._decoratedVectorStoreRecordCollection = decoratedVectorStoreRecordCollection;
        this._textEmbeddingGenerationService = textEmbeddingGenerationService;
        this._options = options ?? new TextEmbeddingVectorStoreRecordCollectionOptions();
        var vectorStoreRecordDefinition = this._options.VectorStoreRecordDefinition ?? VectorStoreRecordPropertyReader.CreateVectorStoreRecordDefinitionFromType(typeof(TRecord), true);

        // Enumerate public properties using configuration or attributes.
        var properties = VectorStoreRecordPropertyReader.FindProperties(typeof(TRecord), vectorStoreRecordDefinition, supportsMultipleVectors: true);

        // Find all the data properties to generate embeddings for.
        var vectorPropertiesDictionary = properties.VectorProperties.ToDictionary(p => p.Name);
        this._dataPropertiesWithEmbeddingProperties = FindDataPropertiesWithEmbeddingProperties(properties.DataProperties, vectorPropertiesDictionary);
    }

    /// <inheritdoc />
    public string CollectionName => this._decoratedVectorStoreRecordCollection.CollectionName;

    /// <inheritdoc />
    public Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        return this._decoratedVectorStoreRecordCollection.CollectionExistsAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task CreateCollectionAsync(CancellationToken cancellationToken = default)
    {
        return this._decoratedVectorStoreRecordCollection.CreateCollectionAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task CreateCollectionIfNotExistsAsync(CancellationToken cancellationToken = default)
    {
        if (!await this.CollectionExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            await this.CreateCollectionAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public Task DeleteCollectionAsync(CancellationToken cancellationToken = default)
    {
        return this._decoratedVectorStoreRecordCollection.DeleteCollectionAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteAsync(TKey key, DeleteRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        return this._decoratedVectorStoreRecordCollection.DeleteAsync(key, options, cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteBatchAsync(IEnumerable<TKey> keys, DeleteRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        return this._decoratedVectorStoreRecordCollection.DeleteBatchAsync(keys, options, cancellationToken);
    }

    /// <inheritdoc />
    public Task<TRecord?> GetAsync(TKey key, GetRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        return this._decoratedVectorStoreRecordCollection.GetAsync(key, options, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TRecord> GetBatchAsync(IEnumerable<TKey> keys, GetRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        return this._decoratedVectorStoreRecordCollection.GetBatchAsync(keys, options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TKey> UpsertAsync(TRecord record, UpsertRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        var recordWithEmbeddings = await this.AddEmbeddingsAsync(record, cancellationToken).ConfigureAwait(false);
        return await this._decoratedVectorStoreRecordCollection.UpsertAsync(recordWithEmbeddings, options, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TKey> UpsertBatchAsync(IEnumerable<TRecord> records, UpsertRecordOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var recordWithEmbeddingsTasks = records.Select(r => this.AddEmbeddingsAsync(r, cancellationToken));
        var recordWithEmbeddings = await Task.WhenAll(recordWithEmbeddingsTasks).ConfigureAwait(false);
        var upserResults = this._decoratedVectorStoreRecordCollection.UpsertBatchAsync(records, options, cancellationToken);
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
