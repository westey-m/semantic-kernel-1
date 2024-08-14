// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// Contains text and the embedding generated from the text to use when searching a vector store.
/// </summary>
/// <typeparam name="TVectorElement">The type of the vector elements.</typeparam>
[Experimental("SKEXP0001")]
public sealed class VectorSearchTextEmbeddingQuery<TVectorElement> : VectorSearchQuery<TVectorElement, string>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VectorSearchTextEmbeddingQuery{TVectorElement}"/> class.
    /// </summary>
    /// <param name="queryText">The text from which the embedding was generated.</param>
    /// <param name="vectorSearchOptions">The options to use when searching the vector store.</param>
    internal VectorSearchTextEmbeddingQuery(string queryText, VectorSearchOptions? vectorSearchOptions)
        : base(null, queryText, vectorSearchOptions)
    {
    }
}
