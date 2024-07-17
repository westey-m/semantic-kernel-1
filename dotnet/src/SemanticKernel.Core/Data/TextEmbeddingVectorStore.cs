// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Threading;
using Microsoft.SemanticKernel.Embeddings;

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// Decorator for a <see cref="IVectorStore"/> that generates embeddings for records on upsert.
/// </summary>
public class TextEmbeddingVectorStore : IVectorStore
{
    /// <summary>The decorated <see cref="IVectorStore"/>.</summary>
    private readonly IVectorStore _decoratedVectorStoreRecordCollection;

    /// <summary>The service to use for generating the embeddings.</summary>
    private readonly ITextEmbeddingGenerationService _textEmbeddingGenerationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextEmbeddingVectorStore"/> class.
    /// </summary>
    /// <param name="decoratedVectorStoreRecordCollection">The decorated <see cref="IVectorStore"/>.</param>
    /// <param name="textEmbeddingGenerationService">The service to use for generating the embeddings.</param>
    public TextEmbeddingVectorStore(IVectorStore decoratedVectorStoreRecordCollection, ITextEmbeddingGenerationService textEmbeddingGenerationService)
    {
        // Verify.
        Verify.NotNull(decoratedVectorStoreRecordCollection);
        Verify.NotNull(textEmbeddingGenerationService);

        // Assign.
        this._decoratedVectorStoreRecordCollection = decoratedVectorStoreRecordCollection;
        this._textEmbeddingGenerationService = textEmbeddingGenerationService;
    }

    /// <inheritdoc />
    public IVectorStoreRecordCollection<TKey, TRecord> GetCollection<TKey, TRecord>(string name, VectorStoreRecordDefinition? vectorStoreRecordDefinition = null) where TRecord : class
    {
        var collection = this._decoratedVectorStoreRecordCollection.GetCollection<TKey, TRecord>(name, vectorStoreRecordDefinition);
        var embeddingStore = new TextEmbeddingVectorStoreRecordCollection<TKey, TRecord>(collection, this._textEmbeddingGenerationService, new TextEmbeddingVectorStoreRecordCollectionOptions { VectorStoreRecordDefinition = vectorStoreRecordDefinition });
        return embeddingStore;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<string> ListCollectionNamesAsync(CancellationToken cancellationToken = default)
    {
        return this._decoratedVectorStoreRecordCollection.ListCollectionNamesAsync(cancellationToken);
    }
}
