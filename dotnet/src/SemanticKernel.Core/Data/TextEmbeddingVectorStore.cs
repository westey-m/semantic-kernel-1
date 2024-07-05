// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Embeddings;

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// Decorator for a <see cref="IVectorStore"/> that generates embeddings for records on upsert.
/// </summary>
public class TextEmbeddingVectorStore : IVectorStore
{
    /// <summary>The decorated <see cref="IVectorStore"/>.</summary>
    private readonly IVectorStore _decoratedVectorRecordStore;

    /// <summary>The service to use for generating the embeddings.</summary>
    private readonly ITextEmbeddingGenerationService _textEmbeddingGenerationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextEmbeddingVectorStore"/> class.
    /// </summary>
    /// <param name="decoratedVectorRecordStore">The decorated <see cref="IVectorStore"/>.</param>
    /// <param name="textEmbeddingGenerationService">The service to use for generating the embeddings.</param>
    public TextEmbeddingVectorStore(IVectorStore decoratedVectorRecordStore, ITextEmbeddingGenerationService textEmbeddingGenerationService)
    {
        // Verify.
        Verify.NotNull(decoratedVectorRecordStore);
        Verify.NotNull(textEmbeddingGenerationService);

        // Assign.
        this._decoratedVectorRecordStore = decoratedVectorRecordStore;
        this._textEmbeddingGenerationService = textEmbeddingGenerationService;
    }

    /// <inheritdoc />
    public Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        return this._decoratedVectorRecordStore.CollectionExistsAsync(name, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IVectorRecordStore<TKey, TRecord>> CreateCollectionAsync<TKey, TRecord>(string name, VectorStoreRecordDefinition? vectorStoreRecordDefinition = null, CancellationToken cancellationToken = default) where TRecord : class
    {
        var recordStore = await this._decoratedVectorRecordStore.CreateCollectionAsync<TKey, TRecord>(name, vectorStoreRecordDefinition, cancellationToken).ConfigureAwait(false);
        var embeddingStore = new TextEmbeddingVectorRecordStore<TKey, TRecord>(recordStore, this._textEmbeddingGenerationService, new TextEmbeddingVectorRecordStoreOptions { VectorStoreRecordDefinition = vectorStoreRecordDefinition });
        return embeddingStore;
    }

    /// <inheritdoc />
    public async Task<IVectorRecordStore<TKey, TRecord>> CreateCollectionIfNotExistsAsync<TKey, TRecord>(string name, VectorStoreRecordDefinition? vectorStoreRecordDefinition = null, CancellationToken cancellationToken = default) where TRecord : class
    {
        var recordStore = await this._decoratedVectorRecordStore.CreateCollectionIfNotExistsAsync<TKey, TRecord>(name, vectorStoreRecordDefinition, cancellationToken).ConfigureAwait(false);
        var embeddingStore = new TextEmbeddingVectorRecordStore<TKey, TRecord>(recordStore, this._textEmbeddingGenerationService, new TextEmbeddingVectorRecordStoreOptions { VectorStoreRecordDefinition = vectorStoreRecordDefinition });
        return embeddingStore;
    }

    /// <inheritdoc />
    public Task DeleteCollectionAsync(string name, CancellationToken cancellationToken = default)
    {
        return this._decoratedVectorRecordStore.DeleteCollectionAsync(name, cancellationToken);
    }

    /// <inheritdoc />
    public IVectorRecordStore<TKey, TRecord> GetCollection<TKey, TRecord>(string name, VectorStoreRecordDefinition? vectorStoreRecordDefinition = null) where TRecord : class
    {
        var recordStore = this._decoratedVectorRecordStore.GetCollection<TKey, TRecord>(name, vectorStoreRecordDefinition);
        var embeddingStore = new TextEmbeddingVectorRecordStore<TKey, TRecord>(recordStore, this._textEmbeddingGenerationService, new TextEmbeddingVectorRecordStoreOptions { VectorStoreRecordDefinition = vectorStoreRecordDefinition });
        return embeddingStore;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<string> ListCollectionNamesAsync(CancellationToken cancellationToken = default)
    {
        return this._decoratedVectorRecordStore.ListCollectionNamesAsync(cancellationToken);
    }
}
