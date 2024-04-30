---
# These are optional elements. Feel free to remove any of them.
status: proposed {proposed | rejected | accepted | deprecated | … | superseded by [ADR-0001](0001-madr-architecture-decisions.md)}
contact: westey-m
date: {YYYY-MM-DD when the decision was last updated}
deciders: sergeymenshykh, markwallace, rbarreto, dmytrostruk, westey-m
consulted: {list everyone whose opinions are sought (typically subject-matter experts); and with whom there is a two-way communication}
informed: {list everyone who is kept up-to-date on progress; and with whom there is a one-way communication}
---

# Updated Memory Connected Design

## Context and Problem Statement

Semantic Kernel has a collection of connectors to popular Vector databases e.g. Azure AI Search, Chroma, Milvus, ...
Each Memory connector implements a memory abstraction defined by Semantic Kernel and allows developers to easily intergrate Vector databases into their applications.
The current abstractions are experimental and the purpose of this ADR is to progress the design of the abstractions so that they can graduate to non experimental status.

### Problems with current design

1. The `IMemoryStore` interface has three responsibilities with different cardinalities and levels of significance to Semantic Kernel.
2. The `IMemoryStore` interface only supports a fixed schema for data storage, retrieval and search, which limits it's usefulness to our customers.

Responsibilities:

|Functional Area|Cardinality|Significance to Semantic Kernel|Avaialble alternatives|
|-|-|-|-|
|Collection/Index management|A single instance per store type|Only indirectly useful, when building a store|SDKs for each individual memory store|
|Data Storage and Retrieval|An instance per store type and model|Directly valueable for storing chat history and indirectly valueable in building a store|kernel-memory sdk/service|
|Vector Search|An instance per store type, model and search type|Directly valueable for RAG scenarios|No alternatives since it's a core scenario for Semantic Kernel|


### Memory Store Today
```cs
interface IMemoryStore
{
    // Collection / Index Management
    CreateCollection
    GetCollections
    DoesCollectionExist
    DeleteCollection

    // Data Storage and Retrieval
    Upsert
    UpsertBatch
    Get
    GetBatch
    Remove
    RemoveBatch

    // Vector Search
    GetNearestMatches
}
```

### Actions

1. The `IMemoryStore` should be split into three different interfaces, one for each responsibility.
2. The **Data Storage and Retrieval** and **Vector Search** areas should allow typed access to data and support any schema that is currently available in the customer's data store.
3. The Collection / Index management area should be evolved to support managing common schemas for built in functionality like chat history, and would work with built in models, filters and plugins.
4. Batching should be removed from **Data Storage and Retrieval** since it's primarily there to support bulk load and index operations and this is outside of the scope of SK.

### Vector Store Cross Store support

|Feature|Azure AI Search|Weaviate|Redis|Chroma|FAISS|Pinecone|LLamaIndex|
|-|-|-|-|-|-|-|-|
|Get Item Suport|Y|Y|Y|Y||Y||
|Batch Operation Support|Y|Y|Y|Y||Y||
|Per Item Results for Batch Operations|Y|Y|Y|N||N||
|Keys of upserted records|Y|Y|N<sup>3</sup>|N<sup>3</sup>||N<sup>3</sup>||
|Keys of removed records|Y||N<sup>3</sup>|N||N||
|Retrieval field selection for gets|Y||Y<sup>4<sup>|P<sup>2</sup>||N||
|Include/Exclude Embeddings for gets|P<sup>1</sup>|Y||Y||N||
|Failure reasons when batch partially fails|Y|Y||N||N||
|Is Key separate from data|N|Y|Y|||||
|Can Generate Ids|N|Y|N|N||Y||
|Field Differentiation|Key,Props,Vectors|Key,Props,Vectors|Key,Props,Vectors|Key,Text,Metadata,Vectors||Key,Props,Vectors||

P = Partial Support

<sup>1</sup> Only if you have the schema, to select the appropriate fields.

<sup>2</sup> Supports broad categories of fields only.

<sup>3</sup> Id is required in request, so can be returned if needed.

<sup>4<sup> No strong typed support when specifying field list.

### Collection/Index management

Schema Comparison
|Feature|Azure AI Search|Weaviate|Redis|
|-|-|-|-|
|Object Properties||||
|Collection Properties||||
|Multiple Vectors Per Record||||
|Vector Algorithm config per Vector Field||||


Open Questions:
- Can we annotate the Vector Store data model with attributes that identify the vectors and the fields they describe, and is it useful?
  - Definitely useful, and there is precedence already with Azure AI Search doing the same.
  - Actually, we'll have to do it, because some databases store regular data values separately from vectors, so we'll need to know what is what.
    - Key
    - Vector
    - Metadata
    - Text
- How would someone pick a collection, if they are storing the same data across many collections, e.g. partitioned by user.
  - Option 1, add a Collections class where you can get one first and then do crud on it.
  - Option 2, add a collection name parameter to each method, like we have now.
- How do we change the key of a record for azure search before wriing without updating the passed in model.
  - This should be done in a decorator, and not in the main class.  If someone really wants this, they can layer it on top, and there can be multiple solutions, e.g. changed passed in model, clone using serialization.
---


{Describe the context and problem statement, e.g., in free form using two to three sentences or in the form of an illustrative story.
You may want to articulate the problem in form of a question and add links to collaboration boards or issue management systems.}

<!-- This is an optional element. Feel free to remove. -->

## Decision Drivers

- Focus on the core value propisition of SK
- Allow break glass scenarios
- 
- {decision driver 1, e.g., a force, facing concern, …}
- {decision driver 2, e.g., a force, facing concern, …}
- … <!-- numbers of drivers can vary -->

## Considered Options

Options Sets:
1. Combined Index and data item management vs separated.
2. Collection name and key value normalization in decorator or main class.
3. Collection name as method param or constructor param.

### Question 1: Combined Index and data item management vs separated.

#### Option 1 - Combined index and data item management

```cs
interface IVectorStore<TDataModel>
{
    Task CreateCollectionAsync(IndexConfig indexConfig, CancellationToken cancellationToken = default);
    Task<IEnumerable<IndexConfig>> GetCollectionsAsync(CancellationToken cancellationToken = default);
    Task DoesCollectionExistAsync(string name, CancellationToken cancellationToken = default);
    Task DeleteCollectionAsync(string name, CancellationToken cancellationToken = default);

    Task UpsertAsync(TDataModel data, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> UpsertBatchAsync(IEnumerable<TDataModel> dataSet, CancellationToken cancellationToken = default);
    Task<TDataModel> GetAsync(string key, bool withEmbedding = false, CancellationToken cancellationToken = default);
    IAsyncEnumerable<TDataModel> GetBatchAsync(IEnumerable<string> keys, bool withEmbeddings = false, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task RemoveBatchAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);
}

class AzureAISearchVectorStore<TDataModel>(
    Azure.Search.Documents.Indexes.SearchIndexClient client,
    Schema schema): IVectorStore<TDataModel>;

class WeaviateVectorStore<TDataModel>(
    WeaviateClient client,
    Schema schema): IVectorStore<TDataModel>;

class RedisVectorStore<TDataModel>(
    StackExchange.Redis.IDatabase database,
    Schema schema): IVectorStore<TDataModel>;
```

#### Option 2 - Separated index and data item management with layered index management

```cs

class AzureAISearchCollectionsManager: IVectorCollectionsManager;
class RedisCollectionsManager: IVectorCollectionsManager;
class WeaviateCollectionsManager: IVectorCollectionsManager;

interface IVectorCollectionsManager
{
    Task CreateCollectionAsync(IndexConfig indexConfig, CancellationToken cancellationToken = default);
    Task<IEnumerable<IndexConfig>> GetCollectionsAsync(CancellationToken cancellationToken = default);
    Task DoesCollectionExistAsync(string name, CancellationToken cancellationToken = default);
    Task DeleteCollectionAsync(string name, CancellationToken cancellationToken = default);
}

class AzureAISearchVectorStore<TDataModel>(IndexConfig indexConfig): IVectorStore<TDataModel>;

interface IVectorStore<TDataModel>
{
    Task UpsertAsync(TDataModel data, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> UpsertBatchAsync(IEnumerable<TDataModel> dataSet, CancellationToken cancellationToken = default);
    Task<TDataModel> GetAsync(string key, bool withEmbedding = false, CancellationToken cancellationToken = default);
    IAsyncEnumerable<TDataModel> GetBatchAsync(IEnumerable<string> keys, bool withEmbeddings = false, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task RemoveBatchAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);
}

```


- {title of option 1}
- {title of option 2}
- {title of option 3}
- … <!-- numbers of options can vary -->

#### Decision Outcome

Chosen option: "{title of option 1}", because
{justification. e.g., only option, which meets k.o. criterion decision driver | which resolves force {force} | … | comes out best (see below)}.

<!-- This is an optional element. Feel free to remove. -->

#### Consequences

- Good, because {positive consequence, e.g., improvement of one or more desired qualities, …}
- Bad, because {negative consequence, e.g., compromising one or more desired qualities, …}
- … <!-- numbers of consequences can vary -->

<!-- This is an optional element. Feel free to remove. -->

#### Validation

{describe how the implementation of/compliance with the ADR is validated. E.g., by a review or an ArchUnit test}

<!-- This is an optional element. Feel free to remove. -->

#### Pros and Cons of the Options

##### {title of option 1}

<!-- This is an optional element. Feel free to remove. -->

{example | description | pointer to more information | …}

- Good, because {argument a}
- Good, because {argument b}
<!-- use "neutral" if the given argument weights neither for good nor bad -->
- Neutral, because {argument c}
- Bad, because {argument d}
- … <!-- numbers of pros and cons can vary -->

##### {title of other option}

{example | description | pointer to more information | …}

- Good, because {argument a}
- Good, because {argument b}
- Neutral, because {argument c}
- Bad, because {argument d}
- …

<!-- This is an optional element. Feel free to remove. -->

###  Question 2: Collection name and key value normalization in decorator or main class.

#### Option 1 - Normalization in main vector store

```cs
    public class AzureAISearchVectorStore<TDataModel> : IVectorStore<TDataModel>
    {
        ...

        // On input.
        var normalizedIndexName = this.NormalizeIndexName(collectionName);
        var encodedId = AzureAISearchMemoryRecord.EncodeId(key);

        ...

        // On output.
        DecodeId(this.Id)

        ...
    }
```

#### Option 2 - Normalization in decorator

```cs
    new KeyNormalizingAISearchVectorStore<MyModel>(
        "keyField",
         new AzureAISearchVectorStore<MyModel>(...));
```

#### Decision Outcome

Chosen option 2 because this behavior mostly makes sense for scenarios where the vector store is both being written to and read from.
If e.g. the data was written using another tool, it may be unlikely that it was encoded using the same mechanism as supported here
and therefore this functionality may not be appropriate. The developer should have the ability to not use this functionality or
provide their own encoding / decoding behavior.

###  Question 3: Collection name as method param or via constructor

#### Option 1 - Collection name as method param

```cs
public async Task<TDataModel?> GetAsync(string collectionName,  VectorStoreGetDocumentOptions? options = default, CancellationToken cancellationToken = default)
```

#### Option 2 - Collection name via construtor

```cs
public async Task<TDataModel?> GetAsync(string key, VectorStoreGetDocumentOptions? options = default, CancellationToken cancellationToken = default)
```

#### Decision Outcome

Chosen option 1, because we need to support customers / databases who use collections as a partitioning strategy, where e.g. the name may be a user id.

## More Information

{You might want to provide additional evidence/confidence for the decision outcome here and/or
document the team agreement on the decision and/or
define when this decision when and how the decision should be realized and if/when it should be re-visited and/or
how the decision is validated.
Links to other decisions and resources might appear here as well.}
