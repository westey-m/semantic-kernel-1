// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Memory;

namespace Microsoft.SemanticKernel.Connectors.AzureAISearch;

/// <summary>
/// Decorator class for <see cref="IVectorDBRecordService{TKey, TDataModel}"/> that normalizes the index names and encodes and decodes the record keys so that any values
/// can be stored in Azure AI Search without violating the constraints of the service.
/// </summary>
/// <typeparam name="TDataModel">The data model to use for adding, updating and retrieving data from storage.</typeparam>
/// <remarks>
/// NOTE: This class mutates the data objects that are passed to it during encoding.
/// </remarks>
public class KeyNormalizingAzureAISearchVectorDBRecordService<TDataModel> : IVectorDBRecordService<string, TDataModel>
    where TDataModel : class
{
    /// <summary>The vector store instance that is being decorated.</summary>
    private readonly IVectorDBRecordService<string, TDataModel> _vectorStore;

    /// <summary>The name of the collection to use with this store if none is provided for any individual operation.</summary>
    private readonly string _defaultCollectionName;

    /// <summary>The name of the key field for the collections that this class is used with.</summary>
    private readonly string _keyFieldName;

    /// <summary>A property info object that points at the key field for the current model, allowing easy reading and writing of this property.</summary>
    private readonly PropertyInfo _keyFieldPropertyInfo;

    /// <summary>
    /// The function that is used to encode the azure ai search record id before it is sent to Azure AI Search.
    /// </summary>
    /// <remarks>
    /// Azure AI Search keys can contain only letters, digits, underscore, dash, equal sign, recommending
    /// to encode values with a URL-safe algorithm or use base64 encoding.
    /// </remarks>
    private readonly Func<string, string> _recordKeyEncoder;

    /// <summary>
    /// The function that is used to decode the azure ai search record id after it is retrieved from Azure AI Search.
    /// </summary>
    /// <remarks>
    /// Azure AI Search keys can contain only letters, digits, underscore, dash, equal sign, recommending
    /// to encode values with a URL-safe algorithm or use base64 encoding.
    /// </remarks>
    private readonly Func<string, string> _recordKeyDecoder;

    /// <summary>
    /// The function that is used to encode the azure ai search index name (collection name parameter).
    /// </summary>
    /// <remarks>Index names have length and character type restrictions.</remarks>
    private readonly Func<string, string> _indexNameEncoder;

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyNormalizingAzureAISearchVectorDBRecordService{TDataModel}"/> class.
    /// </summary>
    /// <param name="vectorStore">The vector store instance that is being decorated.</param>
    /// <param name="defaultCollectionName">The name of the collection to use with this store if none is provided for any individual operation. This should already be encoded when passed in.</param>
    /// <param name="keyFieldName">The name of the key field for the collections that this class is used with.</param>
    /// <param name="recordKeyEncoder">The function that is used to encode the azure ai search record id before it is sent to Azure AI Search.</param>
    /// <param name="recordKeyDecoder">The function that is used to decode the azure ai search record id after it is retrieved from Azure AI Search.</param>
    /// <param name="indexNameEncoder">The function that is used to encode the azure ai search index name (collection name parameter).</param>
    public KeyNormalizingAzureAISearchVectorDBRecordService(
        IVectorDBRecordService<string, TDataModel> vectorStore,
        string defaultCollectionName,
        string keyFieldName,
        Func<string, string> recordKeyEncoder,
        Func<string, string> recordKeyDecoder,
        Func<string, string> indexNameEncoder)
    {
        this._vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        this._defaultCollectionName = string.IsNullOrWhiteSpace(defaultCollectionName) ? throw new ArgumentException("Default collection name is required.", nameof(defaultCollectionName)) : defaultCollectionName;
        this._keyFieldName = string.IsNullOrWhiteSpace(keyFieldName) ? throw new ArgumentException("Key Field name is required.", nameof(keyFieldName)) : keyFieldName;
        this._recordKeyEncoder = recordKeyEncoder ?? throw new ArgumentNullException(nameof(recordKeyEncoder));
        this._recordKeyDecoder = recordKeyDecoder ?? throw new ArgumentNullException(nameof(recordKeyDecoder));
        this._indexNameEncoder = indexNameEncoder ?? throw new ArgumentNullException(nameof(indexNameEncoder));

        this._keyFieldPropertyInfo = typeof(TDataModel).GetProperty(this._keyFieldName, BindingFlags.Public | BindingFlags.Instance);
        if (this._keyFieldPropertyInfo.PropertyType != typeof(string))
        {
            throw new ArgumentException($"Key field must be of type string. Type of {this._keyFieldName} is {this._keyFieldPropertyInfo.PropertyType.FullName}.");
        }
    }

    /// <inheritdoc />
    public async Task<TDataModel?> GetAsync(string key, GetRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        var innerOptions = this.EncodeCollectionName(options);

        var result = await this._vectorStore.GetAsync(
            this._recordKeyEncoder.Invoke(key),
            innerOptions,
            cancellationToken).ConfigureAwait(false);

        if (result != null)
        {
            this.DecodeKeyField(result);
        }

        return result;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TDataModel?> GetBatchAsync(IEnumerable<string> keys, GetRecordOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var innerOptions = this.EncodeCollectionName(options);

        var encodedKeys = keys.Select(this._recordKeyEncoder);
        var results = this._vectorStore.GetBatchAsync(
            encodedKeys,
            innerOptions,
            cancellationToken);

        await foreach (var result in results.ConfigureAwait(false))
        {
            if (result != null)
            {
                this.DecodeKeyField(result);
                yield return result;
            }
            else
            {
                yield return null;
            }
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, RemoveRecordOptions? options = default, CancellationToken cancellationToken = default)
    {
        var innerOptions = this.EncodeCollectionName(options);

        await this._vectorStore.RemoveAsync(
            this._recordKeyEncoder.Invoke(key),
            innerOptions,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveBatchAsync(IEnumerable<string> keys, RemoveRecordOptions? options = default, CancellationToken cancellationToken = default)
    {
        var innerOptions = this.EncodeCollectionName(options);

        await this._vectorStore.RemoveBatchAsync(
            keys.Select(this._recordKeyEncoder),
            innerOptions,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string> UpsertAsync(TDataModel record, UpsertRecordOptions? options = default, CancellationToken cancellationToken = default)
    {
        var innerOptions = this.EncodeCollectionName(options);

        this.EncodeKeyField(record);

        var result = await this._vectorStore.UpsertAsync(
            record,
            innerOptions,
            cancellationToken).ConfigureAwait(false);

        return this._recordKeyDecoder.Invoke(result);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> UpsertBatchAsync(IEnumerable<TDataModel> records, UpsertRecordOptions? options = default, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var innerOptions = this.EncodeCollectionName(options);

        foreach (var record in records)
        {
            this.EncodeKeyField(record);
        }

        var results = this._vectorStore.UpsertBatchAsync(
            records,
            innerOptions,
            cancellationToken);

        await foreach (var result in results.ConfigureAwait(false))
        {
            yield return this._recordKeyDecoder.Invoke(result);
        }
    }

    /// <summary>
    /// Create a new <see cref="GetRecordOptions"/> object with the collection name encoded but with all other properties preserved.
    /// </summary>
    /// <param name="options">The input options to preserve.</param>
    /// <returns>The options with the collection name encoded.</returns>
    private GetRecordOptions EncodeCollectionName(GetRecordOptions? options)
    {
        var collectionName = options?.CollectionName == null ? this._defaultCollectionName : this._indexNameEncoder(options.CollectionName);

        if (options == null)
        {
            return new GetRecordOptions
            {
                CollectionName = collectionName
            };
        }

        return new GetRecordOptions(options)
        {
            CollectionName = collectionName
        };
    }

    /// <summary>
    /// Create a new <see cref="RemoveRecordOptions"/> object with the collection name encoded but with all other properties preserved.
    /// </summary>
    /// <param name="options">The input options to preserve.</param>
    /// <returns>The options with the collection name encoded.</returns>
    private RemoveRecordOptions EncodeCollectionName(RemoveRecordOptions? options)
    {
        var collectionName = options?.CollectionName == null ? this._defaultCollectionName : this._indexNameEncoder(options.CollectionName);

        if (options == null)
        {
            return new RemoveRecordOptions
            {
                CollectionName = collectionName
            };
        }

        return new RemoveRecordOptions(options)
        {
            CollectionName = collectionName
        };
    }

    /// <summary>
    /// Create a new <see cref="UpsertRecordOptions"/> object with the collection name encoded but with all other properties preserved.
    /// </summary>
    /// <param name="options">The input options to preserve.</param>
    /// <returns>The options with the collection name encoded.</returns>
    private UpsertRecordOptions EncodeCollectionName(UpsertRecordOptions? options)
    {
        var collectionName = options?.CollectionName == null ? this._defaultCollectionName : this._indexNameEncoder(options.CollectionName);

        if (options == null)
        {
            return new UpsertRecordOptions
            {
                CollectionName = collectionName
            };
        }

        return new UpsertRecordOptions(options)
        {
            CollectionName = collectionName
        };
    }

    /// <summary>
    /// Encode the key field of the given record and update the record with the new value.
    /// </summary>
    /// <param name="record">The record to update the key field on.</param>
    private void EncodeKeyField(TDataModel record)
    {
        var key = this.GetKeyFieldValue(record);
        var encodedKey = this._recordKeyEncoder.Invoke(key);
        this.SetKeyFieldValue(record, encodedKey);
    }

    /// <summary>
    /// Decode the key field of the given record and update the record with the new value.
    /// </summary>
    /// <param name="record">The record to update the key field on.</param>
    private void DecodeKeyField(TDataModel record)
    {
        var key = this.GetKeyFieldValue(record);
        var decodedKey = this._recordKeyDecoder.Invoke(key);
        this.SetKeyFieldValue(record, decodedKey);
    }

    /// <summary>
    /// Get the key property value from the given record.
    /// </summary>
    /// <param name="record">The record to read the key property's value from.</param>
    /// <returns>The value of the key property.</returns>
    private string GetKeyFieldValue(TDataModel record)
    {
        return (string)this._keyFieldPropertyInfo.GetValue(record);
    }

    /// <summary>
    /// Set the key property on the given record to the given value.
    /// </summary>
    /// <param name="record">The record to update.</param>
    /// <param name="value">The new value for the key property.</param>
    private void SetKeyFieldValue(TDataModel record, string value)
    {
        this._keyFieldPropertyInfo.SetValue(record, value);
    }
}
