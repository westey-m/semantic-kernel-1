// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.SemanticKernel.Memory;
using Qdrant.Client;

namespace Microsoft.SemanticKernel.Connectors.Qdrant;

/// <summary>
/// Provides collection retrieval and deletion for Qdrant.
/// </summary>
public sealed class QdrantVectorCollectionNonSchema : IVectorCollectionNonSchema
{
    /// <summary>Qdrant client that can be used to manage the collections and points in a Qdrant store.</summary>
    private readonly QdrantClient _qdrantClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="QdrantVectorCollectionNonSchema"/> class.
    /// </summary>
    /// <param name="qdrantClient">Qdrant client that can be used to manage the collections and points in a Qdrant store.</param>
    public QdrantVectorCollectionNonSchema(QdrantClient qdrantClient)
    {
        Verify.NotNull(qdrantClient);
        this._qdrantClient = qdrantClient;
    }

    /// <inheritdoc />
    public async Task DeleteCollectionAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            await this._qdrantClient.DeleteCollectionAsync(name, null, cancellationToken).ConfigureAwait(false);
        }
        catch (RpcException ex)
        {
            throw new HttpOperationException(ex.Message, ex);
        }
    }

    /// <inheritdoc />
    public async Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            return await this._qdrantClient.CollectionExistsAsync(name, cancellationToken).ConfigureAwait(false);
        }
        catch (RpcException ex)
        {
            throw new HttpOperationException(ex.Message, ex);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> ListCollectionNamesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> collections;

        try
        {
            collections = await this._qdrantClient.ListCollectionsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (RpcException ex)
        {
            throw new HttpOperationException(ex.Message, ex);
        }

        foreach (var collection in collections)
        {
            yield return collection;
        }
    }
}
