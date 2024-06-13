// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.Memory;

/// <summary>
/// Interface used to create new colletions in a vector store.
/// </summary>
[Experimental("SKEXP0001")]
public interface IVectorCollectionCreate
{
    /// <summary>
    /// Creates a new collection with the given name in the vector store.
    /// </summary>
    /// <param name="name">The name of the new collection.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task that completes when the collection has been created.</returns>
    Task CreateCollectionAsync(string name, CancellationToken cancellationToken = default);
}
