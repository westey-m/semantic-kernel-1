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

Separate search retrieval from store crud, since search is about querying and retrieving a subset of the store.

Allow defining store schema for crud and search params.
Allow defining search result schema for crud.

Create common store schema for storing conversation history.
Allow consumption of custom store schema for storing conversation history.

Allow simple way of defining scenario specific search plugins without needing to write a lot of code, e.g. RetrieveProduct, vs RetrieveUser.

### Today:

```cs
interface IMemoryStore
{
    CreateCollection
    GetCollections
    DoesCollectionExist
    DeleteCollection
    Upsert
    UpsertBatch
    Get
    GetBatch
    Remove
    RemoveBatch
    GetNearestMatches
}

interface IEmbeddingGenerationService<TValue, TEmbedding> : IAIService
{
    GenerateEmbeddingsAsync
}

interface ITextEmbeddingGenerationService: IEmbeddingGenerationService<string, float> {}

interface ISemanticTextMemory
{
    SaveInformationAsync
    SaveReferenceAsync
    GetAsync
    RemoveAsync
    SearchAsync
    GetCollectionsAsync
}
class SemanticTextMemory(IMemoryStore, ITextEmbeddingGenerationService): ISemanticTextMemory {}

class TextMemoryPlugin(ISemanticTextMemory)
{
    RetrieveAsync
    RecallAsync
    SaveAsync
    RemoveAsync
}
```

### Future:

*External Service Clients*

```
Azure.Search.Documents.Indexes.SearchIndexClient
StackExchange.Redis.IDatabase
```

*Optional Data Loading Helpers*

```cs
interface IDataLoader {}
    Loads data from various sources for the purpose of loading into a store.
interface ITextSplitter {}
```

*Storage*

```cs
interface IVectorStore<TDataModel>
{
    CreateCollection
    GetCollections
    DoesCollectionExist
    DeleteCollection
    Upsert
    UpsertBatch
    Get
    GetBatch
    Remove
    RemoveBatch
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

*Reference Hydration*

```cs
interface IRoutingReferenceHydrator
{
    GetData(sourceType, id)
}
interface IReferenceHydrator
{
    GetData(id)
}
```

*Memory Plugins*

We should implement a version of ISemanticTextMemory that uses the new VectorStore Interface and an IAISearchRetriever

*Search Plugins*

From Mark

```cs
interface ITextSearchService: ISearchService
{
    Task<KernelSearchResults<T>> SearchAsync<T>(
        string query,
        SearchExecutionSettings searchSettings,
        CancellationToken cancellationToken = default) where T : class;
}
```

```cs
kernel.SearchPlugins.Add(
    new AzureAINearestNeighborsTextSearchRetriever(
        searchIndexClient,
        embeddingService,
        new ["DescriptionVector"],
        5,
        0.5d,
        new ["HotelName", "Description", "Tags", "Address"],
        "Category eq 'Boutique'",  // Should this be provided on a per call basis and how do we achieve that?
        "postFilter"),
    "Find hotels that match the given description and match the given category", // Plugin semantic description.
    "The hotel description to try and find matches for" // Plugin parameter semantic description.
);

// Do we just need one ITextSearchService, or will VectorSearch require different non-constructor params?
interface IVectorSearchService<TSearchResultRecord>: ISearchService
{
    Task<KernelSearchResults<TSearchResultRecord>> SearchAsync(
        string query,
        SearchExecutionSettings searchSettings,
        CancellationToken cancellationToken = default) where T : class;
}
class AzureAINearestNeighborsTextSearchRetriever<TDataModel, TSearchResultRecord>(
    Azure.Search.Documents.Indexes.SearchIndexClient client,
    IEmbeddingGenerationService embeddingService,
    IEnumerable<string> searchFields,
    int limit,
    double maxDistance,  // Need to come up with a common definition of this if we support a single configuration for different Search DBs.
    IEnumerable<string> selectFields,
    string filter,
    string vectorFilterMode): IAISearchRetriever<TSearchResultRecord>;

class HydratingNearestTextSearchRetriever<TReferenceSearchResultRecord, THydratedSearchResultRecord>(
    IReferenceHydrator hydrator,
    IAISearchRetriever<TReferenceSearchResultRecord> innerRetriever): IAISearchRetriever<THydratedSearchResultRecord>;
```

*Search Result Models*

```cs
class ReferenceAISearchResultRecord
{
    string Description;
    string Name;
    string Id; // (url, id, etc.)
    string SourceType;
    double Score/Distance; // (is this the same thing? For dissimiliarty search are they opposite, e.g. score = 1 - distance?)
}
class DataAISearchResultRecord
{
    string Description;
    string Name;
    string Value;
    double Score/Distance; // (is this the same thing? For dissimiliarty search are they opposite, e.g. score = 1 - distance?)
}
class MemoryAISearchResultRecord;
```

Open Questions:
Do we need to split Structured vs Unstructured Experiences?
    Maybe this is just a different schema / model with a standard model for unstructured with an optional mapping.
Where in this design do we plug in the embedding provider?
    Embedding is based on schema, so it needs to be somewhere where the schema is known.
    Might need to support different embedding providers for different fields in the schema, also supporting different types of data, e.g. text vs image.
Can we annotate the Vector Store data model with attributes that identify the vectors and the fields they describe, and is it useful?


{Describe the context and problem statement, e.g., in free form using two to three sentences or in the form of an illustrative story.
You may want to articulate the problem in form of a question and add links to collaboration boards or issue management systems.}

<!-- This is an optional element. Feel free to remove. -->

## Decision Drivers

- {decision driver 1, e.g., a force, facing concern, …}
- {decision driver 2, e.g., a force, facing concern, …}
- … <!-- numbers of drivers can vary -->

## Considered Options

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
