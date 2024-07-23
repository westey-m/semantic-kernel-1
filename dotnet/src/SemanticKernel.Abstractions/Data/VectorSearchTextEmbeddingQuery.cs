// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// Contains text and the embedding generated from the text to use when searching a vector store.
/// </summary>
/// <typeparam name="TVectorElement">The type of the vector elements.</typeparam>
[Experimental("SKEXP0001")]
public sealed class VectorSearchTextEmbeddingQuery<TVectorElement> : VectorSearchQuery<TVectorElement>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VectorSearchTextEmbeddingQuery{TVectorElement}"/> class.
    /// </summary>
    /// <param name="queryText">The text from which the embedding was generated.</param>
    /// <param name="vector">The vector to use when searching the vector store.</param>
    public VectorSearchTextEmbeddingQuery(string queryText, ReadOnlyMemory<TVectorElement> vector)
    {
        this.QueryText = queryText;
        this.Vector = vector;
    }

    /// <summary>
    /// The text from which the embedding was generated.
    /// </summary>
    public string QueryText { get; init; }
}
