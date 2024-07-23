// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// Contains a vector to use when searching a vector store.
/// </summary>
/// <typeparam name="TVectorElement">The type of the vector elements.</typeparam>
[Experimental("SKEXP0001")]
public class VectorSearchQuery<TVectorElement>
{
    /// <summary>
    /// The vector to use when searching the vector store.
    /// </summary>
    public ReadOnlyMemory<TVectorElement> Vector { get; init; }
}
