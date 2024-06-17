// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Memory;
using NRedisStack.RedisStackCommands;
using StackExchange.Redis;

namespace Microsoft.SemanticKernel.Connectors.Redis;

/// <summary>
/// Provides collection retrieval and deletion for Redis.
/// </summary>
public sealed class RedisVectorCollectionStore : IVectorCollectionStore
{
    /// <summary>The redis database to read/write indices from.</summary>
    private readonly IDatabase _database;

    /// <summary>Used to create new collections in the vector store.</summary>
    private readonly IVectorCollectionCreate _vectorCollectionCreate;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisVectorCollectionStore"/> class.
    /// </summary>
    /// <param name="database">The redis database to read/write indices from.</param>
    /// <param name="vectorCollectionCreate">Used to create new collections in the vector store.</param>
    public RedisVectorCollectionStore(IDatabase database, IVectorCollectionCreate vectorCollectionCreate)
    {
        Verify.NotNull(database);
        Verify.NotNull(vectorCollectionCreate);

        this._database = database;
        this._vectorCollectionCreate = vectorCollectionCreate;
    }

    /// <inheritdoc />
    public Task CreateCollectionAsync(string name, CancellationToken cancellationToken = default)
    {
        return this._vectorCollectionCreate.CreateCollectionAsync(name, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            await this._database.FT().InfoAsync(name).ConfigureAwait(false);
            return true;
        }
        catch (RedisServerException ex) when (ex.Message.Contains("Unknown index name"))
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task DeleteCollectionAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            await this._database.FT().DropIndexAsync(name).ConfigureAwait(false);
        }
        catch (RedisServerException ex)
        {
            throw new HttpOperationException(ex.Message, ex);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> ListCollectionNamesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        RedisResult[] listResult;

        try
        {
            listResult = await this._database.FT()._ListAsync().ConfigureAwait(false);
        }
        catch (RedisServerException ex)
        {
            throw new HttpOperationException(ex.Message, ex);
        }

        foreach (var item in listResult)
        {
            yield return item.ToString();
        }
    }
}
