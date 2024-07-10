# 1. Separate collection and record stores.

```cs
// Create is not parameterized, Instance closes over schema.
// Con: You can list, delete and check for existance of collections that doesn't match the schema.
interface IConfiguredVectorCollectionStore
{
    Task CreateCollectionAsync(string name, CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> ListCollectionNamesAsync(CancellationToken cancellationToken = default);
    Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = default);
    Task DeleteCollectionAsync(string name, CancellationToken cancellationToken = default);
}

// Create is parameterized, Instance (mostly) Schema Agnostic.
// Con: Certain db specific options may still need to be passed into the constructor, making this not 100% schema agnostic.
interface IVectorCollectionStore
{
    Task CreateCollectionAsync(string name, VectorStoreRecordDefinition vectorStoreRecordDefinition, CancellationToken cancellationToken);
    Task CreateCollectionAsync<TRecord>(string name, CancellationToken cancellationToken);

    IAsyncEnumerable<string> ListCollectionNamesAsync(CancellationToken cancellationToken = default);
    Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = default);
    Task DeleteCollectionAsync(string name, CancellationToken cancellationToken = default);
}

// Closes over schema.
// Supports interacting with multiple collections with the same instance by passing a CollectionName to the Options.
interface IVectorRecordStore<TKey, TRecord>
{
    Task<TRecord?> GetAsync(TKey key, GetRecordOptions? options = default, CancellationToken cancellationToken = default);
    Task DeleteAsync(TKey key, DeleteRecordOptions? options = default, CancellationToken cancellationToken = default);
    Task<TKey> UpsertAsync(TRecord record, UpsertRecordOptions? options = default, CancellationToken cancellationToken = default);

    // Batch methods omitted for brevity.
}
```

# 2. Collection store acts as factory for record store.

```cs
// This effectively represents a view over the SDK client's capabilities
// Con: Each store has its own configuration settings, and some are hard to pick defaults for, so constructing a RecordStore via the abstraction can cause us to pick defaults that leave many users unhappy, or provide a callback that can provide these, or forced to break out of the abstraction.
public interface IVectorStore
{
    // record definition is optional because we presumably support inferring it from the user-provided CLR type (attributes)

    // Not on collection option
    //IVectorRecordCollection<TKey, TRecord> GetCollection<TKey, TRecord>(string name, VectorStoreRecordDefinition? vectorStoreRecordDefinition = null);
    //Task<IVectorRecordCollection<TKey, TRecord>> GetIfExistsCollection<TKey, TRecord>(string name, VectorStoreRecordDefinition? vectorStoreRecordDefinition = null);
    //Task<IVectorRecordCollection<TKey, TRecord>> CreateCollectionAsync<TKey, TRecord>(string name, VectorStoreRecordDefinition? vectorStoreRecordDefinition = null);
    //Task<IVectorRecordCollection<TKey, TRecord>> CreateCollectionIfNotExistsAsync<TKey, TRecord>(string name, VectorStoreRecordDefinition? vectorStoreRecordDefinition = null);
    //Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = default));
    //Task DeleteCollectionAsync(string name, CancellationToken cancellationToken = default));

    // On Collection Option
    IVectorRecordCollection<TKey, TRecord> GetCollection<TKey, TRecord>(string name, VectorStoreRecordDefinition? vectorStoreRecordDefinition = null);

    IAsyncEnumerable<string> ListCollectionNamesAsync(CancellationToken cancellationToken = default));
}

// Does not support interacting with multiple collections with the same instance.
public interface IVectorRecordCollection<TKey, TRecord>
{
    public string Name { get; }

    // On Collection Option
    Task CreateCollectionAsync();
    Task<bool> CreateCollectionIfNotExistsAsync();
    Task<bool> CollectionExistsAsync();
    Task DeleteCollectionAsync();

    // Data manipulation
    Task<TRecord?> GetAsync(TKey key, GetRecordOptions? options = default, CancellationToken cancellationToken = default);
    IAsyncEnumerable<TRecord> GetBatchAsync(IEnumerable<TKey> keys, GetRecordOptions? options = default, CancellationToken cancellationToken = default);
    Task DeleteAsync(TKey key, DeleteRecordOptions? options = default, CancellationToken cancellationToken = default);
    Task DeleteBatchAsync(IEnumerable<TKey> keys, DeleteRecordOptions? options = default, CancellationToken cancellationToken = default);
    Task<TKey> UpsertAsync(TRecord record, UpsertRecordOptions? options = default, CancellationToken cancellationToken = default);
    IAsyncEnumerable<TKey> UpsertBatchAsync(IEnumerable<TRecord> records, UpsertRecordOptions? options = default, CancellationToken cancellationToken = default);
}
```

# 3. Simply combine everything.

```cs
// Create is parameterized.
// Con: Collection management is schema agnostic, while record management is not.
interface IVectorStore<TKey, TRecord>
{
    Task CreateCollectionAsync(string name, VectorStoreRecordDefinition vectorStoreRecordDefinition, CancellationToken cancellationToken);
    Task CreateCollectionAsync<TRecord>(string name, CancellationToken cancellationToken);

    IAsyncEnumerable<string> ListCollectionNamesAsync(CancellationToken cancellationToken = default);
    Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = default);
    Task DeleteCollectionAsync(string name, CancellationToken cancellationToken = default);

    Task<TRecord?> GetAsync(TKey key, GetRecordOptions? options = default, CancellationToken cancellationToken = default);
    Task DeleteAsync(TKey key, DeleteRecordOptions? options = default, CancellationToken cancellationToken = default);
    Task<TKey> UpsertAsync(TRecord record, UpsertRecordOptions? options = default, CancellationToken cancellationToken = default);

    // Batch methods omitted for brevity.
}

// Create is not parameterized.
// Con: Create Collection and record operations are schema aware, while other collection operations are schema agnostic.
// Con: You can list, delete and check for existance of collections that doesn't match the schema.
interface IVectorStore<TKey, TRecord>
{
    Task CreateCollectionAsync(string name, CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> ListCollectionNamesAsync(CancellationToken cancellationToken = default);
    Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = default);
    Task DeleteCollectionAsync(string name, CancellationToken cancellationToken = default);

    Task<TRecord?> GetAsync(TKey key, GetRecordOptions? options = default, CancellationToken cancellationToken = default);
    Task DeleteAsync(TKey key, DeleteRecordOptions? options = default, CancellationToken cancellationToken = default);
    Task<TKey> UpsertAsync(TRecord record, UpsertRecordOptions? options = default, CancellationToken cancellationToken = default);

    // Batch methods omitted for brevity.
}

```

# Record Store Options for different DBs

```cs
public sealed class AzureAISearchVectorRecordStoreOptions<TRecord>
{
    public string? DefaultCollectionName { get; init; } = null;
    public VectorStoreRecordDefinition? VectorStoreRecordDefinition { get; init; } = null;

    public IVectorStoreRecordMapper<TRecord, JsonObject>? JsonObjectCustomMapper { get; init; } = null;
    public JsonSerializerOptions? JsonSerializerOptions { get; init; } = null;
}

public sealed class QdrantVectorRecordStoreOptions<TRecord>
{
    public string? DefaultCollectionName { get; init; } = null;
    public VectorStoreRecordDefinition? VectorStoreRecordDefinition { get; init; } = null;

    public IVectorStoreRecordMapper<TRecord, PointStruct>? PointStructCustomMapper { get; init; } = null;

    public bool HasNamedVectors { get; set; } = false;
}

public sealed class RedisVectorRecordStoreOptions<TRecord>
{
    public string? DefaultCollectionName { get; init; } = null;
    public VectorStoreRecordDefinition? VectorStoreRecordDefinition { get; init; } = null;

    public IVectorStoreRecordMapper<TRecord, (string Key, JsonNode Node)>? JsonNodeCustomMapper { get; init; } = null;
    public JsonSerializerOptions? JsonSerializerOptions { get; init; } = null;

    public bool PrefixCollectionNameToKeyNames { get; init; } = false;
}
```