// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// Contains text and the embedding generated from the text to use when searching a vector store.
/// </summary>
/// <typeparam name="TVectorElement">The type of the vector elements.</typeparam>
[Experimental("SKEXP0001")]
public sealed class VectorSearchVectorQuery<TVectorElement> : VectorSearchQuery<TVectorElement, object>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VectorSearchTextEmbeddingQuery{TVectorElement}"/> class.
    /// </summary>
    /// <param name="vector">The vector to use when searching the vector store.</param>
    /// <param name="vectorSearchOptions">The options to use when searching the vector store.</param>
    internal VectorSearchVectorQuery(ReadOnlyMemory<TVectorElement> vector, VectorSearchOptions? vectorSearchOptions)
        : base(vector, null, vectorSearchOptions)
    {
    }
}
