// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Embeddings;

namespace Microsoft.SemanticKernel.Agents.Memory;

public class VectorDataMemoryDocumentStore<TKey> : MemoryDocumentStore
    where TKey : notnull
{
    private readonly IVectorStore _vectorStore;
    private readonly ITextEmbeddingGenerationService _textEmbeddingGenerationService;
    private readonly string _storageNamespace;
    private readonly int _vectorDimensions;
    private readonly Lazy<IVectorStoreRecordCollection<TKey, MemoryDocument<TKey>>> _vectorStoreRecordCollection;
    private bool _collectionInitialized = false;

    public VectorDataMemoryDocumentStore(IVectorStore vectorStore, ITextEmbeddingGenerationService textEmbeddingGenerationService, string collectionName, string storageNamespace, int vectorDimensions = 1536)
    {
        VectorStoreRecordDefinition _memoryDocumentDefinition = new()
        {
            Properties = new List<VectorStoreRecordProperty>()
            {
                new VectorStoreRecordKeyProperty("Key", typeof(TKey)),
                new VectorStoreRecordDataProperty("Namespace", typeof(string)),
                new VectorStoreRecordDataProperty("Name", typeof(string)),
                new VectorStoreRecordDataProperty("Category", typeof(string)),
                new VectorStoreRecordDataProperty("MemoryText", typeof(string)),
                new VectorStoreRecordVectorProperty("MemoryTextEmbedding", typeof(ReadOnlyMemory<float>)) { Dimensions = vectorDimensions },
            }
        };

        this._vectorStore = vectorStore;
        this._textEmbeddingGenerationService = textEmbeddingGenerationService;
        this._storageNamespace = storageNamespace;
        this._vectorDimensions = vectorDimensions;
        this._vectorStoreRecordCollection = new Lazy<IVectorStoreRecordCollection<TKey, MemoryDocument<TKey>>>(() =>
            this._vectorStore.GetCollection<TKey, MemoryDocument<TKey>>(collectionName, this._memoryDocumentDefinition));
    }

    /// <inheritdoc/>
    public override async Task<string?> GetMemoryAsync(string documentName, CancellationToken cancellationToken = default)
    {
        var vectorStoreRecordCollection = this._vectorStoreRecordCollection.Value;
        if (!this._collectionInitialized)
        {
            await vectorStoreRecordCollection.CreateCollectionIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
            this._collectionInitialized = true;
        }

        ReadOnlyMemory<float> vector = new(new float[this._vectorDimensions]);
        var options = new VectorSearchOptions
        {
            Filter = new VectorSearchFilter()
                .EqualTo(nameof(MemoryDocument<string>.Name), documentName)
                .EqualTo(nameof(MemoryDocument<string>.Namespace), this._storageNamespace),
        };
        var searchResult = await vectorStoreRecordCollection.VectorizedSearchAsync(vector, cancellationToken: cancellationToken).ConfigureAwait(false);
        var results = await searchResult.Results.ToListAsync(cancellationToken).ConfigureAwait(false);

        if (results.Count == 0)
        {
            return null;
        }

        return results[0].Record.MemoryText;
    }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<string> SearchForMatchingMemories(string query, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var vectorStoreRecordCollection = this._vectorStoreRecordCollection.Value;
        if (!this._collectionInitialized)
        {
            await vectorStoreRecordCollection.CreateCollectionIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
            this._collectionInitialized = true;
        }

        var vector = await this._textEmbeddingGenerationService.GenerateEmbeddingAsync(query, cancellationToken: cancellationToken).ConfigureAwait(false);
        var options = new VectorSearchOptions
        {
            Filter = new VectorSearchFilter()
                .EqualTo(nameof(MemoryDocument<string>.Namespace), this._storageNamespace),
        };
        var searchResult = await vectorStoreRecordCollection.VectorizedSearchAsync(vector, cancellationToken: cancellationToken).ConfigureAwait(false);

        await foreach (var result in searchResult.Results.ConfigureAwait(false))
        {
            yield return result.Record.MemoryText;
        }
    }

    /// <inheritdoc/>
    public override async Task SaveMemoryAsync(string documentName, string memoryText, CancellationToken cancellationToken = default)
    {
        var vectorStoreRecordCollection = this._vectorStoreRecordCollection.Value;
        if (!this._collectionInitialized)
        {
            await vectorStoreRecordCollection.CreateCollectionIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
            this._collectionInitialized = true;
        }

        var vector = await this._textEmbeddingGenerationService.GenerateEmbeddingAsync(
            string.IsNullOrWhiteSpace(memoryText) ? "Empty" : memoryText,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var memoryDocument = new MemoryDocument<TKey>
        {
            Key = GenerateUniqueKey<TKey>(),
            Namespace = this._storageNamespace,
            Name = documentName,
            MemoryText = memoryText,
            MemoryTextEmbedding = vector,
        };

        await vectorStoreRecordCollection.UpsertAsync(memoryDocument, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override Task SaveMemoryAsync(string memoryText, CancellationToken cancellationToken = default)
    {
        return this.SaveMemoryAsync(null!, memoryText, cancellationToken);
    }

    private readonly VectorStoreRecordDefinition _memoryDocumentDefinition = new()
    {
        Properties = new List<VectorStoreRecordProperty>()
        {
            new VectorStoreRecordKeyProperty("Key", typeof(TKey)),
            new VectorStoreRecordDataProperty("Namespace", typeof(string)),
            new VectorStoreRecordDataProperty("Name", typeof(string)),
            new VectorStoreRecordDataProperty("Category", typeof(string)),
            new VectorStoreRecordDataProperty("MemoryText", typeof(string)),
            new VectorStoreRecordVectorProperty("MemoryTextEmbedding", typeof(ReadOnlyMemory<float>)),
        }
    };

    private static TDocumentKey GenerateUniqueKey<TDocumentKey>()
        => typeof(TDocumentKey) switch
        {
            _ when typeof(TDocumentKey) == typeof(string) => (TDocumentKey)(object)Guid.NewGuid().ToString(),
            _ when typeof(TDocumentKey) == typeof(Guid) => (TDocumentKey)(object)Guid.NewGuid(),

            _ => throw new NotSupportedException($"Unsupported key of type '{typeof(TDocumentKey).Name}'")
        };

    private class MemoryDocument<TDocumentKey>
    {
        /// <summary>
        /// Gets or sets a unique identifier for the memory document.
        /// </summary>
        public TDocumentKey Key { get; set; } = default!;

        /// <summary>
        /// Gets or sets the namespace for the memory document.
        /// </summary>
        /// <remarks>
        /// A namespace is a logical grouping of memory documents, e.g. may include a user id to scope the memory to a specific user.
        /// </remarks>
        public string Namespace { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an optional name for the memory document.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an optional category for the memory document.
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the actual memory content as text.
        /// </summary>
        public string MemoryText { get; set; } = string.Empty;

        public ReadOnlyMemory<float> MemoryTextEmbedding { get; set; }
    }
}
