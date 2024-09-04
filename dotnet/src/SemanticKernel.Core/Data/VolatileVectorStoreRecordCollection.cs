// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics.Tensors;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// Service for storing and retrieving vector records, that uses an in memory dictionary as the underlying storage.
/// </summary>
/// <typeparam name="TKey">The data type of the record key.</typeparam>
/// <typeparam name="TRecord">The data model to use for adding, updating and retrieving data from storage.</typeparam>
[Experimental("SKEXP0001")]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public sealed class VolatileVectorStoreRecordCollection<TKey, TRecord> : IVectorStoreRecordCollection<TKey, TRecord>, IVectorSearch<TRecord>
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
    where TKey : notnull
    where TRecord : class
{
    /// <summary>A set of types that vectors on the provided model may have.</summary>
    private static readonly HashSet<Type> s_supportedVectorTypes =
    [
        typeof(ReadOnlyMemory<float>),
        typeof(ReadOnlyMemory<float>?),
    ];

    /// <summary>Internal storage for the record collection.</summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<object, object>> _internalCollection;

    /// <summary>Optional configuration options for this class.</summary>
    private readonly VolatileVectorStoreRecordCollectionOptions _options;

    /// <summary>The name of the collection that this <see cref="VolatileVectorStoreRecordCollection{TKey,TRecord}"/> will access.</summary>
    private readonly string _collectionName;

    /// <summary>A dictionary of vector properties on the provided model, keyed by the property name.</summary>
    private readonly Dictionary<string, VectorStoreRecordVectorProperty> _vectorProperties;

    /// <summary>A dictionary of vector property info objects on the provided model, keyed by the property name.</summary>
    private readonly Dictionary<string, PropertyInfo> _vectorPropertiesInfo;

    /// <summary>A property info object that points at the key property for the current model, allowing easy reading and writing of this property.</summary>
    private readonly PropertyInfo _keyPropertyInfo;

    /// <summary>The first vector field for the collections that this class is used with.</summary>
    private readonly PropertyInfo? _firstVectorPropertyInfo = null;

    /// <summary>
    /// Initializes a new instance of the <see cref="VolatileVectorStoreRecordCollection{TKey,TRecord}"/> class.
    /// </summary>
    /// <param name="collectionName">The name of the collection that this <see cref="VolatileVectorStoreRecordCollection{TKey,TRecord}"/> will access.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    public VolatileVectorStoreRecordCollection(string collectionName, VolatileVectorStoreRecordCollectionOptions? options = default)
    {
        // Verify.
        Verify.NotNullOrWhiteSpace(collectionName);

        // Assign.
        this._collectionName = collectionName;
        this._internalCollection = new();
        this._options = options ?? new VolatileVectorStoreRecordCollectionOptions();
        var vectorStoreRecordDefinition = this._options.VectorStoreRecordDefinition ?? VectorStoreRecordPropertyReader.CreateVectorStoreRecordDefinitionFromType(typeof(TRecord), true);

        // Validate property types.
        var properties = VectorStoreRecordPropertyReader.SplitDefinitionAndVerify(typeof(TRecord).Name, vectorStoreRecordDefinition, supportsMultipleVectors: true, requiresAtLeastOneVector: false);
        VectorStoreRecordPropertyReader.VerifyPropertyTypes(properties.VectorProperties, s_supportedVectorTypes, "Vector");
        this._vectorProperties = properties.VectorProperties.ToDictionary(x => x.DataModelPropertyName);
        this._vectorPropertiesInfo = properties.VectorProperties
            .Select(x => x.DataModelPropertyName)
            .Select(x => typeof(TRecord).GetProperty(x) ?? throw new ArgumentException($"Vector property {x} not found on {typeof(TRecord).Name}"))
            .ToDictionary(x => x.Name);

        this._keyPropertyInfo = typeof(TRecord).GetProperty(properties.KeyProperty.DataModelPropertyName) ?? throw new ArgumentException($"Key property {properties.KeyProperty.DataModelPropertyName} not found on {typeof(TRecord).Name}");
        if (properties.VectorProperties.Count > 0)
        {
            var firstVectorPropertyName = properties.VectorProperties.First().DataModelPropertyName;
            this._firstVectorPropertyInfo = this._vectorPropertiesInfo[firstVectorPropertyName];
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VolatileVectorStoreRecordCollection{TKey,TRecord}"/> class.
    /// </summary>
    /// <param name="internalCollection">Allows passing in the dictionary used for storage, for testing purposes.</param>
    /// <param name="collectionName">The name of the collection that this <see cref="VolatileVectorStoreRecordCollection{TKey,TRecord}"/> will access.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    internal VolatileVectorStoreRecordCollection(ConcurrentDictionary<string, ConcurrentDictionary<object, object>> internalCollection, string collectionName, VolatileVectorStoreRecordCollectionOptions? options = default)
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
    public Task CreateCollectionAsync(CancellationToken cancellationToken = default)
    {
        this._internalCollection.TryAdd(this._collectionName, new ConcurrentDictionary<object, object>());
        return Task.CompletedTask;
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
        this._internalCollection.TryRemove(this._collectionName, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<TRecord?> GetAsync(TKey key, GetRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        var collectionDictionary = this.GetCollectionDictionary();

        if (collectionDictionary.TryGetValue(key, out var record))
        {
            return Task.FromResult<TRecord?>(record as TRecord);
        }

        return Task.FromResult<TRecord?>(null);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TRecord> GetBatchAsync(IEnumerable<TKey> keys, GetRecordOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
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
    public Task DeleteAsync(TKey key, DeleteRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        var collectionDictionary = this.GetCollectionDictionary();

        collectionDictionary.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteBatchAsync(IEnumerable<TKey> keys, DeleteRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        var collectionDictionary = this.GetCollectionDictionary();

        foreach (var key in keys)
        {
            collectionDictionary.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<TKey> UpsertAsync(TRecord record, UpsertRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        var collectionDictionary = this.GetCollectionDictionary();

        var key = (TKey)this._keyPropertyInfo.GetValue(record)!;
        collectionDictionary.AddOrUpdate(key!, record, (key, currentValue) => record);

        return Task.FromResult(key!);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TKey> UpsertBatchAsync(IEnumerable<TRecord> records, UpsertRecordOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var record in records)
        {
            yield return await this.UpsertAsync(record, options, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<VectorSearchResult<TRecord>> SearchAsync(VectorSearchQuery vectorQuery, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (this._firstVectorPropertyInfo is null)
        {
            throw new InvalidOperationException("The collection does not have any vector fields, so vector search is not possible.");
        }

        if (vectorQuery is VectorizedSearchQuery<ReadOnlyMemory<float>> floatVectorQuery)
        {
            // Resolve options and get requested vector property or first as default.
            var internalOptions = floatVectorQuery.SearchOptions ?? Data.VectorSearchOptions.Default;
            PropertyInfo? vectorPropertyInfo;
            if (internalOptions.VectorFieldName is not null)
            {
                if (!this._vectorPropertiesInfo.TryGetValue(internalOptions.VectorFieldName, out vectorPropertyInfo))
                {
                    throw new InvalidOperationException($"The collection does not have a vector field named '{internalOptions.VectorFieldName}', so vector search is not possible.");
                }
            }
            else
            {
                vectorPropertyInfo = this._firstVectorPropertyInfo;
            }

            var vectorProperty = this._vectorProperties[vectorPropertyInfo.Name];

            // Compare each vector in the dictionary with the provided vector in parallel batches.
            var results = await ExecuteInParallelAsync<KeyValuePair<object, object>, (object record, float score)?>(
                this.GetCollectionDictionary(),
                (dictionaryEntry) =>
                {
                    var vector = (ReadOnlyMemory<float>?)this._firstVectorPropertyInfo.GetValue(dictionaryEntry.Value);
                    if (vector is not null)
                    {
                        var similarity = CompareVectors(floatVectorQuery.Vector.Span, vector.Value.Span, vectorProperty.DistanceFunction);
                        return (dictionaryEntry.Value, similarity);
                    }

                    return null;
                },
                4,
                cancellationToken).ConfigureAwait(false);

            // Get the non-null results, sort them appropriately for the selected distance function and return the requested page.
            var nonNullResults = results.Where(x => x.HasValue).Select(x => x!.Value);
            var sortedScoredResults = ShouldSortDescending(vectorProperty.DistanceFunction) ?
                nonNullResults.OrderByDescending(x => x.score) :
                nonNullResults.OrderBy(x => x.score);

            foreach (var scoredResult in sortedScoredResults.Skip(internalOptions.Offset).Take(internalOptions.Limit))
            {
                yield return new VectorSearchResult<TRecord>((TRecord)scoredResult.record, scoredResult.score);
            }
        }
        else
        {
            throw new NotSupportedException($"A {nameof(VectorSearchQuery)} of type {vectorQuery.QueryType} is not supported by the Volatile connector.");
        }
    }

    /// <summary>
    /// Get the collection dictionary from the internal storage, throws if it does not exist.
    /// </summary>
    /// <returns>The retrieved collection dictionary.</returns>
    private ConcurrentDictionary<object, object> GetCollectionDictionary()
    {
        if (!this._internalCollection.TryGetValue(this._collectionName, out var collectionDictionary))
        {
            throw new VectorStoreOperationException($"Call to vector store failed. Collection '{this._collectionName}' does not exist.");
        }

        return collectionDictionary;
    }

    /// <summary>
    /// Compare the two vectors using the specified distance function.
    /// </summary>
    /// <param name="x">The first vector to compare.</param>
    /// <param name="y">The second vector to compare.</param>
    /// <param name="distanceFunction">The distance function to use for comparison.</param>
    /// <returns>The score of the comparison.</returns>
    /// <exception cref="NotSupportedException">Thrown when the distance function is not supported.</exception>
    private static float CompareVectors(ReadOnlySpan<float> x, ReadOnlySpan<float> y, string? distanceFunction)
    {
        switch (distanceFunction)
        {
            case null:
            case DistanceFunction.CosineSimilarity:
            case DistanceFunction.CosineDistance:
                return TensorPrimitives.CosineSimilarity(x, y);
            case DistanceFunction.DotProductSimilarity:
                return TensorPrimitives.Dot(x, y);
            case DistanceFunction.EuclideanDistance:
                return TensorPrimitives.Distance(x, y);
            default:
                throw new NotSupportedException($"The distance function '{distanceFunction}' is not supported by the Volatile connector.");
        }
    }

    /// <summary>
    /// Indicates whether result ordering should be descending or ascending, to get most similar results at the top, based on the distance function.
    /// </summary>
    /// <param name="distanceFunction">The distance function to use for comparison.</param>
    /// <returns>Whether to order descending or ascending.</returns>
    /// <exception cref="NotSupportedException">Thrown when the distance function is not supported.</exception>
    private static bool ShouldSortDescending(string? distanceFunction)
    {
        switch (distanceFunction)
        {
            case null:
            case DistanceFunction.CosineSimilarity:
            case DistanceFunction.CosineDistance:
            case DistanceFunction.DotProductSimilarity:
                return true;
            case DistanceFunction.EuclideanDistance:
                return false;
            default:
                throw new NotSupportedException($"The distance function '{distanceFunction}' is not supported by the Volatile connector.");
        }
    }

    /// <summary>
    /// Converts the provided score into the correct result depending on the distance function.
    /// The main purpose here is to convert from cosine similarity to cosine distance if cosine distance is requested,
    /// since the two are inversely related and the <see cref="TensorPrimitives"/> only supports cosine similarity so
    /// we are using cosine similarity for both similarity and distance.
    /// </summary>
    /// <param name="score">The score to convert.</param>
    /// <param name="distanceFunction">The distance function to use for comparison.</param>
    /// <returns>Whether to order descending or ascending.</returns>
    /// <exception cref="NotSupportedException">Thrown when the distance function is not supported.</exception>
    private static double ConvertScore(double score, string? distanceFunction)
    {
        switch (distanceFunction)
        {
            case DistanceFunction.CosineDistance:
                return 1 - score;
            case null:
            case DistanceFunction.CosineSimilarity:
            case DistanceFunction.DotProductSimilarity:
            case DistanceFunction.EuclideanDistance:
                return score;
            default:
                throw new NotSupportedException($"The distance function '{distanceFunction}' is not supported by the Volatile connector.");
        }
    }

    /// <summary>
    /// Execute the provided action in parallel on the provided items, with a maximum parallelism.
    /// </summary>
    /// <typeparam name="TInput">The type of items in the input enumerable.</typeparam>
    /// <typeparam name="TResult">The type of results to return for each input item.</typeparam>
    /// <param name="items">The input items to process in parallel.</param>
    /// <param name="action">The action to use to process each item.</param>
    /// <param name="maxParallelism">The maximum parallel processing queues.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The processed results.</returns>
    private static Task<TResult[]> ExecuteInParallelAsync<TInput, TResult>(IEnumerable<TInput> items, Func<TInput, TResult> action, int maxParallelism, CancellationToken cancellationToken)
    {
        var allTasks = new List<Task<TResult>>();
        var taskQueues = new Task[maxParallelism];
        var taskIndex = 0;

        foreach (var item in items)
        {
            var task = new Task<TResult>(() => action(item));
            allTasks.Add(task);

            var taskQueueIndex = taskIndex++ % maxParallelism;

            var existingTask = taskQueues[taskQueueIndex];
            if (existingTask is not null)
            {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed - we want to run the task in parallel.
#pragma warning disable VSTHRD110 // Observe result of async calls
                existingTask.ContinueWith(_ => task.Start(), cancellationToken, TaskContinuationOptions.None, TaskScheduler.Default);
#pragma warning restore VSTHRD110 // Observe result of async calls
#pragma warning restore CS4014
            }
            else
            {
                task.Start();
            }

            taskQueues[taskQueueIndex] = task;
        }

        return Task.WhenAll(allTasks);
    }
}
