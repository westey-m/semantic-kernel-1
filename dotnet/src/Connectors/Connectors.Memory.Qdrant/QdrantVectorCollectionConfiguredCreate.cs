// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Memory;

namespace Microsoft.SemanticKernel.Connectors.Qdrant;

/// <summary>
/// Class that can create a new collection in qdrant using a provided configuration.
/// </summary>
public class QdrantVectorCollectionConfiguredCreate : IVectorCollectionCreate
{
    /// <inheritdoc />
    public Task CreateCollectionAsync(string name, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
