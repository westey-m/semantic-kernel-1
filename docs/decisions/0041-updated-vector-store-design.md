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
3. The Collection / Index management area should provide an abstraction across the different types of data stores. A common schema format can be provided via a common interface and used to create/update/delete indices / collections.

### Vector Store Cross Store support

|Feature|Azure AI Search|Weaviate|Redis|
|-|-|-|-|
|Per Item Results for Batch Operations|Y|||
|Keys of upserted records|Y|||
|Keys of removed records||||
|Retrieval field selection for gets|Y|||
|Failure reasons when batch partially fails|Y|||


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

- {decision driver 1, e.g., a force, facing concern, …}
- {decision driver 2, e.g., a force, facing concern, …}
- … <!-- numbers of drivers can vary -->

## Considered Options

### Option 1 - Combined index and data item management

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

### Option 2 - Separated index and data item management with layered index management

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

class CollectionSchema
{

}

class FieldSchema
{
    string Name { get; init; };
    string Type { get; init; };
}

    // Azure AI Search Options:
    "searchable": false, 
    "filterable": true, 
    "retrievable": true, 
    "sortable": false, 
    "facetable": false,
    "key": true

```


- {title of option 1}
- {title of option 2}
- {title of option 3}
- … <!-- numbers of options can vary -->

## Decision Outcome

Chosen option: "{title of option 1}", because
{justification. e.g., only option, which meets k.o. criterion decision driver | which resolves force {force} | … | comes out best (see below)}.

<!-- This is an optional element. Feel free to remove. -->

### Consequences

- Good, because {positive consequence, e.g., improvement of one or more desired qualities, …}
- Bad, because {negative consequence, e.g., compromising one or more desired qualities, …}
- … <!-- numbers of consequences can vary -->

<!-- This is an optional element. Feel free to remove. -->

## Validation

{describe how the implementation of/compliance with the ADR is validated. E.g., by a review or an ArchUnit test}

<!-- This is an optional element. Feel free to remove. -->

## Pros and Cons of the Options

### {title of option 1}

<!-- This is an optional element. Feel free to remove. -->

{example | description | pointer to more information | …}

- Good, because {argument a}
- Good, because {argument b}
<!-- use "neutral" if the given argument weights neither for good nor bad -->
- Neutral, because {argument c}
- Bad, because {argument d}
- … <!-- numbers of pros and cons can vary -->

### {title of other option}

{example | description | pointer to more information | …}

- Good, because {argument a}
- Good, because {argument b}
- Neutral, because {argument c}
- Bad, because {argument d}
- …

<!-- This is an optional element. Feel free to remove. -->

## More Information

{You might want to provide additional evidence/confidence for the decision outcome here and/or
document the team agreement on the decision and/or
define when this decision when and how the decision should be realized and if/when it should be re-visited and/or
how the decision is validated.
Links to other decisions and resources might appear here as well.}
