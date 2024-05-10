---
# These are optional elements. Feel free to remove any of them.
status: proposed
contact: westey-m
date: 2024-05-01
deciders: sergeymenshykh, markwallace, rbarreto, dmytrostruk, westey-m
consulted: 
informed: 
---

# Updated Memory Connector Design

## Context and Problem Statement

Semantic Kernel has a collection of connectors to popular Vector databases e.g. Azure AI Search, Chroma, Milvus, ...
Each Memory connector implements a memory abstraction defined by Semantic Kernel and allows developers to easily intergrate Vector databases into their applications.
The current abstractions are experimental and the purpose of this ADR is to progress the design of the abstractions so that they can graduate to non experimental status.

### Problems with current design

1. The `IMemoryStore` interface has three responsibilities with different cardinalities and levels of significance to Semantic Kernel.
2. The `IMemoryStore` interface only supports a fixed schema for data storage, retrieval and search, which limits its usability by customers with existing data sets.
2. The `IMemoryStore` implementations are opinionated around key encoding / decoding and collection name sanitization, which limits its usability by customers with existing data sets.

Responsibilities:

|Functional Area|Cardinality|Significance to Semantic Kernel|Avaialble alternatives|
|-|-|-|-|
|Collection/Index management|An implementation per store type and model|Only indirectly useful, when building a store|SDKs for each individual memory store|
|Data Storage and Retrieval|An implementation per store type|Directly valueable for storing chat history and indirectly valueable in building a store|kernel-memory sdk/service|
|Vector Search|An implementation per store type, model and search type|Directly valueable for RAG scenarios|No alternatives since it's a core scenario for Semantic Kernel|


### Memory Store Today
```cs
interface IMemoryStore
{
    // Collection / Index Management
    Task CreateCollectionAsync(string collectionName, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> GetCollectionsAsync(CancellationToken cancellationToken = default);
    Task<bool> DoesCollectionExistAsync(string collectionName, CancellationToken cancellationToken = default);
    Task DeleteCollectionAsync(string collectionName, CancellationToken cancellationToken = default);

    // Data Storage and Retrieval
    Task<string> UpsertAsync(string collectionName, MemoryRecord record, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> UpsertBatchAsync(string collectionName, IEnumerable<MemoryRecord> records, CancellationToken cancellationToken = default);
    Task<MemoryRecord?> GetAsync(string collectionName, string key, bool withEmbedding = false, CancellationToken cancellationToken = default);
    IAsyncEnumerable<MemoryRecord> GetBatchAsync(string collectionName, IEnumerable<string> keys, bool withVectors = false, CancellationToken cancellationToken = default);
    Task RemoveAsync(string collectionName, string key, CancellationToken cancellationToken = default);
    Task RemoveBatchAsync(string collectionName, IEnumerable<string> keys, CancellationToken cancellationToken = default);

    // Vector Search
    IAsyncEnumerable<(MemoryRecord, double)> GetNearestMatchesAsync(
        string collectionName,
        ReadOnlyMemory<float> embedding,
        int limit,
        double minRelevanceScore = 0.0,
        bool withVectors = false,
        CancellationToken cancellationToken = default);

    Task<(MemoryRecord, double)?> GetNearestMatchAsync(
        string collectionName,
        ReadOnlyMemory<float> embedding,
        double minRelevanceScore = 0.0,
        bool withEmbedding = false,
        CancellationToken cancellationToken = default);
}
```

### Actions

1. The `IMemoryStore` should be split into three different interfaces, one for each responsibility.
2. The **Data Storage and Retrieval** and **Vector Search** areas should allow typed access to data and support any schema that is currently available in the customer's data store.
3. The Collection / Index management area should be evolved to support managing common schemas for built in functionality like chat history, and would work with built in models, filters and plugins.
4. Batching should be removed from **Data Storage and Retrieval** since it's primarily there to support bulk load and index operations and this is outside of the scope of SK.
5. Remove opinionated behaviors from connectors. The opinionated behavior limits the ability of these connectors to be used with pre-created vector databases. As far as possible these behaviors should be moved into decorators.  Examples of opinionated behaviors:
    1. The AzureAISearch connector encodes keys before storing and decodes them after retrieval since keys in Azure AI Search supports a limited set of characters.
    2. The AzureAISearch connector sanitizes collection names before using them, since Azure AI Search supports a limited set of characters.
    3. The Redis connector prepends the collection name on to the front of keys before storing records and also registers the collection name as a prefix for records to be indexed by the index.

### New Designs

The separation between collection/index management and record management with batching support removed.

```mermaid
---
title: SK Collection/Index and Vector management
---
classDiagram
    note for IVectorDBRecordsService "Can manage records for any scenario"
    note for IVectorDBCollectionsService "Can manage collections and\nindexes for core scenarios"

    namespace SKAbstractions{
        class IVectorDBCollectionsService{
            <<interface>>
            +CreateChatHistoryCollection
            +CreateSemanticCacheCollection
            +GetCollections
            +DeleteCollection
        }

        class IVectorDBRecordsService~TModel~{
            <<interface>>
            +Upsert(TModel record) string
            +Get(string key) TModel
            +Remove(string key) string
        }
    }

    namespace AzureAIMemory{
        class AzureAISearchVectorDBCollectionsService{
        }

        class AzureAISearchVectorDBRecordsService{
        }
    }

    namespace RedisMemory{
        class RedisVectorDBCollectionsService{
        }

        class RedisVectorDBRecordsService{
        }
    }

    IVectorDBCollectionsService <|-- AzureAISearchVectorDBCollectionsService
    IVectorDBCollectionsService <|-- RedisVectorDBCollectionsService

    IVectorDBRecordsService <|-- AzureAISearchVectorDBRecordsService
    IVectorDBRecordsService <|-- RedisVectorDBRecordsService
```

How to use your own schema with core sk functionality.

```mermaid
---
title: Chat History Break Glass
---
classDiagram
    note for IVectorDBRecordsService "Can manage records for any scenario"
    note for IVectorDBCollectionsService "Can manage collections and\nindexes for core scenarios"
    note for CustomerHistoryVectorDBRecordsService "Decorator class for IVectorDBRecordsService that maps\nbetween the customer model to our model"
    note for CustomerVectorDBCollectionManagement "Creates indices using\nCustomer requirements"

    namespace SKAbstractions{
        class IVectorDBCollectionsService{
            <<interface>>
            +CreateChatHistoryCollection
            +CreateSemanticCacheCollection
            +GetCollections
            +DeleteCollection
        }

        class IVectorDBRecordsService~TModel~{
            <<interface>>
            +Upsert(TModel record) string
            +Get(string key) TModel
            +Remove(string key) string
        }

        class ISemanticTextMemory{
            <<interface>>
            +SaveInformationAsync()
            +SaveReferenceAsync()
            +GetAsync()
            +RemoveAsync()
            +SearchAsync()
            +GetCollectionsAsync()
        }
    }

    namespace CustomerProject{
        class CustomerHistoryModel{
            +string text
            +float[] vector
            +Dictionary~string, string~ properties
        }

        class CustomerHistoryVectorDBRecordsService{
            -IVectorDBRecordsService~CustomerHistoryModel~ _store
            +Upsert(ChatHistoryModel record) string
            +Get(string key) ChatHistoryModel
            +Remove(string key) string
        }

        class CustomerVectorDBCollectionManagement{
            +CreateChatHistoryCollection
            +CreateSemanticCacheCollection
            +GetCollections
            +DeleteCollection
        }
    }

    namespace SKCore{
        class SemanticTextMemory{
            -IVectorDBRecordsService~ChatHistoryModel~ _vectorDBRecordsService
            -IVectorDBCollectionsService _collectionsService
            -ITextEmbeddingGenerationService _embeddingGenerationService
        }

        class ChatHistoryPlugin{
            -ISemanticTextMemory memory
        }

        class ChatHistoryModel{
            +string message
            +float[] embedding
            +Dictionary~string, string~ metadata
        }
    }

    IVectorDBRecordsService <|-- CustomerHistoryVectorDBRecordsService
    IVectorDBRecordsService <.. CustomerHistoryVectorDBRecordsService
    CustomerHistoryModel <.. CustomerHistoryVectorDBRecordsService
    ChatHistoryModel <.. CustomerHistoryVectorDBRecordsService
    IVectorDBCollectionsService <|-- CustomerVectorDBCollectionManagement

    ChatHistoryModel <.. SemanticTextMemory
    IVectorDBRecordsService <.. SemanticTextMemory
    IVectorDBCollectionsService <.. SemanticTextMemory

    ISemanticTextMemory <.. ChatHistoryPlugin
```

### Vector Store Cross Store support

A comparison of the different ways in which stores implement storage capabilities to help drive decisions:

|Feature|Azure AI Search|Weaviate|Redis|Chroma|FAISS|Pinecone|LLamaIndex|PostgreSql|Qdrant|Milvus|
|-|-|-|-|-|-|-|-|-|-|-|
|Get Item Suport|Y|Y|Y|Y||Y||Y|Y|Y|
|Batch Operation Support|Y|Y|Y|Y||Y||||Y|
|Per Item Results for Batch Operations|Y|Y|Y|N||N|||||
|Keys of upserted records|Y|Y|N<sup>3</sup>|N<sup>3</sup>||N<sup>3</sup>||||Y|
|Keys of removed records|Y||N<sup>3</sup>|N||N||||N<sup>3</sup>|
|Retrieval field selection for gets|Y||Y<sup>4<sup>|P<sup>2</sup>||N||Y|Y|Y|
|Include/Exclude Embeddings for gets|P<sup>1</sup>|Y|Y<sup>4,1<sup>|Y||N||P<sup>1</sup>|Y|N|
|Failure reasons when batch partially fails|Y|Y|Y|N||N|||||
|Is Key separate from data|N|Y|Y|Y||Y||N|Y|N|
|Can Generate Ids|N|Y|N|N||Y||Y|N|Y|
|Can Generate Embedding|Not Available Via API yet|Y|N|Client Side Abstraction|||||N||
|Field Differentiation|Fields|Key, Props, Vectors|Key, Fields|Key, Documents, Metadata, Vectors||Key, Metadata, SparseValues, Vectors||Fields|Key, Props(Payload), Vectors|Fields|
|Index to Collection|1 to 1|1 to 1|1 to many|1 to 1|-|1 to 1|-|1 to 1|1 to 1|1 to 1|
|Id Type|String|UUID|string with collection name prefix|string||string|UUID|64Bit Int / UUID / ULID|64Bit Unsigned Int / UUID|Int64 / varchar|
|Supported Vector Types|Collection(Edm.Single)|float32|FLOAT32 and FLOAT64|||[Rust f32](https://docs.pinecone.io/troubleshooting/embedding-values-changed-when-upserted)||[single-precision (4 byte float) / half-precision (2 byte float) / binary (1bit) / sparse vectors (4 bytes)](https://github.com/pgvector/pgvector?tab=readme-ov-file#pgvector)|UInt8 / Float32|Binary / Float32 / Float16 / BFloat16 / SparseFloat|
|Supported Distance Functions|[Cosine / dot prod / euclidean dist (l2 norm)](https://learn.microsoft.com/en-us/azure/search/vector-search-ranking#similarity-metrics-used-to-measure-nearness)|[Cosine dist / dot prod / Squared L2 dist / hamming (num of diffs) / manhattan dist](https://weaviate.io/developers/weaviate/config-refs/distances#available-distance-metrics)|[Euclidean dist (L2) / Inner prod (IP) / Cosine dist](https://redis.io/docs/latest/develop/interact/search-and-query/advanced-concepts/vectors/)|[Squared L2 / Inner prod / Cosine similarity](https://docs.trychroma.com/usage-guide#changing-the-distance-function)||[cosine sim / euclidean dist / dot prod](https://docs.pinecone.io/reference/api/control-plane/create_index)||[L2 dist / inner prod / cosine dist / L1 dist / Hamming dist / Jaccard dist](https://github.com/pgvector/pgvector?tab=readme-ov-file#pgvector)|[Dot prod / Cosine sim / Euclidean dist (L2) / Manhattan dist](https://qdrant.tech/documentation/concepts/search/)|[Cosine sim / Euclidean dist / Inner Prod](https://milvus.io/docs/index-vector-fields.md)|
|Supported index types|[Exhaustive KNN / HNSW](https://learn.microsoft.com/en-us/azure/search/vector-search-ranking#algorithms-used-in-vector-search)|[HNSW / Flat / Dynamic](https://weaviate.io/developers/weaviate/config-refs/schema/vector-index)|[HNSW / FLAT](https://redis.io/docs/latest/develop/interact/search-and-query/advanced-concepts/vectors/#create-a-vector-field)|[HNSW not configurable](https://cookbook.chromadb.dev/core/concepts/#vector-index-hnsw-index)||[PGA](https://www.pinecone.io/blog/hnsw-not-enough/)||[HNSW / IVFFlat](https://github.com/pgvector/pgvector?tab=readme-ov-file#indexing)|[HNSW for dense](https://qdrant.tech/documentation/concepts/indexing/#vector-index)|<p>[In Memory: FLAT / IVF_FLAT / IVF_SQ8 / IVF_PQ / HNSW / SCANN](https://milvus.io/docs/index.md)</p><p>[On Disk: DiskANN](https://milvus.io/docs/disk_index.md)</p><p>[GPU: GPU_CAGRA / GPU_IVF_FLAT / GPU_IVF_PQ / GPU_BRUTE_FORCE](https://milvus.io/docs/gpu_index.md)</p>|

Footnotes:
- P = Partial Support
- <sup>1</sup> Only if you have the schema, to select the appropriate fields.
- <sup>2</sup> Supports broad categories of fields only.
- <sup>3</sup> Id is required in request, so can be returned if needed.
- <sup>4</sup> No strong typed support when specifying field list.
- HNSW = Hierarchical Navigable Small World (HNSW performs an [approximate nearest neighbor (ANN)](https://learn.microsoft.com/en-us/azure/search/vector-search-overview#approximate-nearest-neighbors) search)
- KNN = k-nearest neighbors (performs a brute-force search that scans the entire vector space)
- IVFFlat = Inverted File with Flat Compression (This index type uses approximate nearest neighbor search (ANNS) to provide fast searches)
- Weaviate Dynamic = Starts as flat and switches to HNSW if the number of objects exceed a limit
- PGA = [Pinecone Graph Algorithm](https://www.pinecone.io/blog/hnsw-not-enough/)

### Support for different storage schemas

The different stores vary in many ways around how data is organized.
- Some just store a record with fields on it, where fields can be a key or a data field or a vector and their type is determined at collection creation time.
- Others separate fields by type when interacting with the api, e.g. you have to specify a key explicitly, put metadata into a metadata dictionary and put vectors into a vector array.

This means that we have to know the category of field for each field in the record.
I'm therefore proposing that we use attributes to annotate the model indicating the category of field.

```cs
    public record HotelShortInfo(
        [property: VectorDBRecordModelKey, JsonPropertyName("hotel-id")] string HotelId,
        [property: VectorDBRecordModelMetadata, JsonPropertyName("hotel-name")] string HotelName,
        [property: VectorDBRecordModelData, JsonPropertyName("description")] string Description,
        [property: VectorDBRecordModelVector(DataField = "Description"), JsonPropertyName("description-embeddings")] ReadOnlyMemory<float>? DescriptionEmbeddings);
```

## Decision Drivers

From GitHub Issue:
- API surface must be easy to use and intuitive
- Alignment with other patterns in the SK
- - Design must allow Memory Plugins to be easily instantiated with any connector
- Design must support all Kernel content types
- Design must allow for database specific configuration
- All NFR's to be production ready are implemented
- Basic CRUD operations must be supported so that connectors can be used in a polymorphic manner
- Official Database Clients must be used where available
- Dynamic database schema must be supported
- Dependency injection must be supported
- Azure-ML YAML format must be supported
- Breaking glass scenarios must be supported

Additional:
- Focus on the core value propisition of SK


## Considered Questions

1. Combined collection and record management vs separated.
2. Collection name and key value normalization in decorator or main class.
3. Collection name as method param or constructor param.

### Question 1: Combined collection and record management vs separated.

#### Option 1 - Combined collection and record management

```cs
interface IVectorDBRecordsService<TDataModel>
{
    Task CreateCollectionAsync(CollectionConfig collectionConfig, CancellationToken cancellationToken = default);
    Task<IEnumerable<CollectionConfig>> GetCollectionsAsync(CancellationToken cancellationToken = default);
    Task<bool> DoesCollectionExistAsync(string name, CancellationToken cancellationToken = default);
    Task DeleteCollectionAsync(string name, CancellationToken cancellationToken = default);

    Task UpsertAsync(TDataModel data, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> UpsertBatchAsync(IEnumerable<TDataModel> dataSet, CancellationToken cancellationToken = default);
    Task<TDataModel> GetAsync(string key, bool withEmbedding = false, CancellationToken cancellationToken = default);
    IAsyncEnumerable<TDataModel> GetBatchAsync(IEnumerable<string> keys, bool withVectors = false, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task RemoveBatchAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);
}

class AzureAISearchVectorDBRecordsService<TDataModel>(
    Azure.Search.Documents.Indexes.SearchIndexClient client,
    Schema schema): IVectorDBRecordsService<TDataModel>;

class WeaviateVectorDBRecordsService<TDataModel>(
    WeaviateClient client,
    Schema schema): IVectorDBRecordsService<TDataModel>;

class RedisVectorDBRecordsService<TDataModel>(
    StackExchange.Redis.IDatabase database,
    Schema schema): IVectorDBRecordsService<TDataModel>;
```

#### Option 2 - Separated collection and record management

```cs

interface IVectorDBCollectionsService
{
    virtual Task CreateChatHistoryCollectionAsync(string name, CancellationToken cancellationToken = default);
    virtual Task CreateSemanticCacheCollectionAsync(string name, CancellationToken cancellationToken = default);

    Task<IEnumerable<string>> GetCollectionsAsync(CancellationToken cancellationToken = default);
    Task<bool> DoesCollectionExistAsync(string name, CancellationToken cancellationToken = default);
    Task DeleteCollectionAsync(string name, CancellationToken cancellationToken = default);
}

class AzureAISearchVectorDBCollectionsService: IVectorDBCollectionsService;
class RedisVectorDBCollectionsService: IVectorDBCollectionsService;
class WeaviateVectorDBCollectionsService: IVectorDBCollectionsService;

// Customers can inherit from our implementations and replace just the creation scenarios to match their schemas.
class CustomerCollectionsService: AzureAISearchVectorDBCollectionsService, IVectorDBCollectionsService;

// We can also create implementations that create indices based on an MLIndex specification.
class MLIndexAzureAISearchVectorDBCollectionsService(MLIndex mlIndexSpec): AzureAISearchVectorDBCollectionsService, IVectorDBCollectionsService;

interface IVectorDBRecordsService<TDataModel>
{
    Task<TDataModel?> GetAsync(string key, VectorDBRecordsServiceGetDocumentOptions? options = default, CancellationToken cancellationToken = default);
    Task<string> RemoveAsync(string key, VectorDBRecordsServiceRemoveDocumentOptions? options = default, CancellationToken cancellationToken = default);
    Task<string> UpsertAsync(TDataModel record, VectorDBRecordsServiceUpsertDocumentOptions? options = default, CancellationToken cancellationToken = default);
}

class AzureAISearchVectorDBRecordsService<TDataModel>(): IVectorDBRecordsService<TDataModel>;
```

#### Option 3 - Separated collection and record management with collection create separate from other operations.

Vector store same as option 2 so not repeated for brevity.

```cs

interface IVectorDBCollectionCreationService
{
    virtual Task CreateCollectionAsync(string name, CancellationToken cancellationToken = default);
}

class AzureAISearchChatHistoryCollectionCreationService: IVectorDBCollectionCreationService;
class AzureAISearchSemanticCacheCollectionCreationService: IVectorDBCollectionCreationService;

// Customers can create their own creation scenarios to match their schemas, but can continue to use our get, does exist and delete class.
class CustomerChatHistoryCollectionCreationService: IVectorDBCollectionCreationService;

interface IVectorDBCollectionsService
{
    Task<IEnumerable<string>> GetCollectionsAsync(CancellationToken cancellationToken = default);
    Task<bool> DoesCollectionExistAsync(string name, CancellationToken cancellationToken = default);
    Task DeleteCollectionAsync(string name, CancellationToken cancellationToken = default);
}

class AzureAISearchVectorDBCollectionsService: IVectorDBCollectionsService;
class RedisVectorDBCollectionsService: IVectorDBCollectionsService;
class WeaviateVectorDBCollectionsService: IVectorDBCollectionsService;

```

#### Option 4 - Separated collection and record management with collection create separate from other operations, with collection management aggregation class on top.

Variation on option 3. 

```cs

interface IVectorDBCollectionCreationService
{
    virtual Task CreateCollectionAsync(string name, CancellationToken cancellationToken = default);
}

interface IVectorDBCollectionsService : IVectorDBCollectionCreationService
{
    Task<IEnumerable<string>> GetCollectionsAsync(CancellationToken cancellationToken = default);
    Task<bool> DoesCollectionExistAsync(string name, CancellationToken cancellationToken = default);
    Task DeleteCollectionAsync(string name, CancellationToken cancellationToken = default);
}

class AzureAISearchChatHistoryVectorDBCollectionCreationService: IVectorDBCollectionCreationService;
class AzureAISearchSemanticCacheVectorDBCollectionCreationService: IVectorDBCollectionCreationService;

// Base abstract class that forwards create operation to provided creation service.
abstract class VectorDBCollectionsService(IVectorDBCollectionCreationService creationService): IVectorDBCollectionsService
{
    public Task CreateCollectionAsync(string name, CancellationToken cancellationToken = default) { return creationService.CreateCollectionAsync(name, cancellationToken); }
    public abstract Task<IEnumerable<string>> GetCollectionsAsync(CancellationToken cancellationToken = default);
    public abstract Task<bool> DoesCollectionExistAsync(string name, CancellationToken cancellationToken = default);
    public abstract Task DeleteCollectionAsync(string name, CancellationToken cancellationToken = default);
}

// Collections service implementations, that can work with different creation implementations.
class AzureAISearchVectorDBCollectionsService(IVectorDBCollectionCreationService creationService): VectorDBCollectionsService(creationService);
class RedisVectorDBCollectionsService(IVectorDBCollectionCreationService creationService): VectorDBCollectionsService(creationService);
class WeaviateVectorDBCollectionsService(IVectorDBCollectionCreationService creationService): VectorDBCollectionsService(creationService);

// Collections service implementation, that has it's own built in creation implementation.
class ContosoProductsVectorDBCollectionsService(): IVectorDBCollectionsService;

```

#### Option 5 - Separated collection and record management with collection create separate from other operations, with overall aggregation class on top.

Same as option 3 / 4, plus:

```cs

interface IVectorService : IVectorDBCollectionCreationService, IVectorDBCollectionsService, IVectorDBRecordsService
{    
}

class AzureAISearchChatHistoryVectorService: IVectorService;
class AzureAISearchSemanticCacheVectorService: IVectorService;

```

#### Decision Outcome

Chosen option: 4 + 5.

- Collection setup and configuration varies considerably across different databases.
- Collection setup and configuration outside of some core supported scenarios is not part of the value proposition of SK.
- Vector storage, even with custom schemas can be supported using a single implementation.
- We will therefore need to support multiple collection service implementations per store type and a single vector store implementation per store type.
- At the same time we can layer interfaces on top that allow easy combined access to colleciton and record management.


###  Question 2: Collection name and key value normalization in decorator or main class.

#### Option 1 - Normalization in main vector store

- Pros: Simple
- Cons: The normalization needs to vary separately from the vector store, so this will not work

```cs
    public class AzureAISearchVectorDBRecordsService<TDataModel> : IVectorDBRecordsService<TDataModel>
    {
        ...

        // On input.
        var normalizedCollectionName = this.NormalizeCollectionName(collectionName);
        var encodedId = AzureAISearchMemoryRecord.EncodeId(key);

        ...

        // On output.
        DecodeId(this.Id)

        ...
    }
```

#### Option 2 - Normalization in decorator

- Pros: Allows normalization to vary separately from the vector store.
- Pros: No code executed when no normalization required.
- Pros: Easy to package matching encoders/decoders together.
- Pros: Easier to obsolete encoding/normalization as a concept.
- Cons: Need to implement the full VectorDBRecordsService interface.

```cs
    new KeyNormalizingAISearchVectorDBRecordsService<MyModel>(
        "keyField",
         new AzureAISearchVectorDBRecordsService<MyModel>(...));
```

#### Option 3 - Normalization via optional function parameters to vector store constructor

- Pros: Allows normalization to vary separately from the vector store.
- Pros: No need to implement the full VectorDBRecordsService interface.
- Pros: Can modify values on serialization without changing the incoming record.
- Cons: Harder to package matching encoders/decoders together.

```cs
public class AzureAISearchVectorDBRecordsService<TDataModel>(StoreOptions options);

public class StoreOptions
{
    public Func<string, string>? EncodeKey { get; init; }
    public Func<string, string>? DecodeKey { get; init; }
    public Func<string, string>? SanitizeCollectionName { get; init; }
}
```

#### Decision Outcome

Option 2 / 3 should work. Leaning towards 2, but let's discuss.

Option 1 won't work because if e.g. the data was written using another tool, it may be unlikely that it was encoded using the same mechanism as supported here
and therefore this functionality may not be appropriate. The developer should have the ability to not use this functionality or
provide their own encoding / decoding behavior.

###  Question 3: Collection name as method param or via constructor or either

#### Option 1 - Collection name as method param

```cs
public class MyMemoryStore()
{
    public async Task<TDataModel?> GetAsync(string collectionName, string key, VectorDBRecordsServiceGetDocumentOptions? options = default, CancellationToken cancellationToken = default);
}
```

#### Option 2 - Collection name via construtor

```cs
public class MyMemoryStore(string defaultCollectionName)
{
    public async Task<TDataModel?> GetAsync(string key, VectorDBRecordsServiceGetDocumentOptions? options = default, CancellationToken cancellationToken = default);
}
```

#### Option 3 - Collection name via either

```cs
public class MyMemoryStore(string defaultCollectionName)
{
    public async Task<TDataModel?> GetAsync(string key, VectorDBRecordsServiceGetDocumentOptions? options = default, CancellationToken cancellationToken = default);
}

public class VectorDBRecordsServiceGetDocumentOptions
{
    public string CollectionName { get; init; };
}
```

#### Decision Outcome

Chosen option 3, because we need to support customers / databases who use collections as a partitioning strategy, where e.g. the name may be a user id.
At the same time, the sdk should be easy to use for people who don't need this.
So requiring customers to provide a default collection name, but they can optionally choose to provide a per operation collection name per operation as well.
This also has the benefit of having an options object for each operation, so making the api future proof for extensibility.

## More Information

{You might want to provide additional evidence/confidence for the decision outcome here and/or
document the team agreement on the decision and/or
define when this decision when and how the decision should be realized and if/when it should be re-visited and/or
how the decision is validated.
Links to other decisions and resources might appear here as well.}

## Roadmap

### Vector Store

1. Release Vector Store public interface and implementations for Azure AI Search, Qdrant and Redis.
2. Add support for registering vector stores with SK container to allow automatic dependency injection.
3. Add Vector Store implementations for remaining stores.

### Collection Management

4. Release Collection Management public interface and implementations for Azure AI Search, Qdrant and Redis.
5. Add support for registering collection management with SK container to allow automatic dependency injection.
6. Add Collection Management implementations for remaining stores.

### Collection Creation

7. Release Collection Creation public interface.
8. Add support for registering collection creation with SK container to allow automatic dependency injection.

### First Party Memory Features

9. Add first party implementations and storage models for chat history with Collection Creation for supported stores.
10. Add first party implementations and storage models for semantic caching with Collection Creation for supported stores.
11. Add samples showing how to map between first party implementations and custom models.
