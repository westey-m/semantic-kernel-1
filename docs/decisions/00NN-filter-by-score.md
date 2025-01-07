---
# These are optional elements. Feel free to remove any of them.
status: {proposed | rejected | accepted | deprecated | � | superseded by [ADR-0001](0001-madr-architecture-decisions.md)}
contact: westey-m
date: 2025-01-06
deciders: {list everyone involved in the decision}
consulted: {list everyone whose opinions are sought (typically subject-matter experts); and with whom there is a two-way communication}
informed: {list everyone who is kept up-to-date on progress; and with whom there is a one-way communication}
---

# Support Filtering by Score in Vector Search in the Vector Store Abstractions

## Context and Problem Statement

Many use cases require a minimum similarity when retrieving results using vector search.
E.g. there is often no point in including results that have a very low score, since they are unlikely to be valuable for RAG scenarios.
Some databases support the ability to filter by a minimum similarity where any results less similar than that minimum would not be returned to the client.

## Feature Comparision between different Vector DBs

|Feature|Azure AI Search|Weaviate|Redis|Chroma|Pinecone|PostgreSql|Qdrant|Milvus|Elasticsearch|CosmosDB NoSql|MongoDB|
|-|-|-|-|-|-|-|-|-|-|-|-|
|Supports theshold in api|Y (in preview)|Y|Y (but not in combination with KNN)|-|Can't find support in docs|Y|Y|Y (both upper and lower bound)|Y|[Seems possible, needs testing](https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/vector-search#perform-vector-search-with-queries-using-vectordistance)|[Supported via hack with limitations](https://www.mongodb.com/community/forums/t/search-for-all-vectors-with-similarity-above-threshold/274637?msockid=3e0a5061bf0868e0138e4508be446965)|
|Threshold naming|[threshold](https://learn.microsoft.com/en-us/azure/search/vector-search-how-to-query?tabs=query-2024-07-01%2Cbuiltin-portal#set-thresholds-to-exclude-low-scoring-results-preview)|max [distance](https://weaviate.io/developers/weaviate/search/similarity#set-a-similarity-threshold)|[radius](https://redis.io/docs/latest/develop/interact/search-and-query/advanced-concepts/vectors/) also see sample [here](https://redis.io/docs/latest/develop/interact/search-and-query/query/vector-search/)|-|-|['distance' in docs, not used in syntax](https://github.com/pgvector/pgvector?tab=readme-ov-file#querying)|[score_threshold](https://api.qdrant.tech/api-reference/search/query-points#request.body.score_threshold)|[radius](https://milvus.io/docs/range-search.md)|minimum [similarity](https://www.elastic.co/guide/en/elasticsearch/reference/current/query-dsl-knn-query.html#knn-query-top-level-parameters)|-|-|

Langchain uses `score_threshold` for naming, see [here](https://api.python.langchain.com/en/latest/vectorstores/langchain_community.vectorstores.qdrant.Qdrant.html#langchain_community.vectorstores.qdrant.Qdrant.asimilarity_search_by_vector).
However, it doesn't support the parameter consistently, e.g. it's simply missing for [pinecone](https://api.python.langchain.com/en/latest/vectorstores/langchain_pinecone.vectorstores.PineconeVectorStore.html#langchain_pinecone.vectorstores.PineconeVectorStore.asimilarity_search_by_vector), which I don't believe support this feature.

## Distance Function score ranges

|Distance Function|Range|Most Similar Value|Least Similar Value|
|-|-|-|-|
|Cosine Similarity|-1 <= d <= 1|1 (identical)|-1 (opposite)|
|Cosine Distance|0 <= d <= 2|0 (identical)|2 (opposite)|
|Dot Product Similarity (dot product)|-∞ <= d < ∞|∞|-∞|
|Dot Product Distance (negative dot product)|-∞ <= d < ∞|-∞|∞|
|Euclidean Distance (l2-norm)|0 <= d < ∞|0|∞|
|Euclidean Squared Distance (l2-squared)|0 <= d < ∞|0|∞|
|Hamming|0 <= d < dims|0|dims|
|Manhattan Distance|0 <= d < ∞|0|∞|

The ranges of values that scores can have differ significantly depending on the type of distance function used as is shown in the above table.
E.g. closely related vectors may be in the range of 0.7 - 1 for cosine similarity but 0 to 0.3 for cosine distance. For hamming closely related
may be between 0 and 10.
It's therefore not possible to use a single score that means 'closely related' when switching between different distance functions.

Switching your distance function will therefore typically require changing your score threshold as well.
This is confusing to users, so it'll be important to document this very clearly.

## Decision Drivers

- Ease of use
- Implementation cost
- Simplicity

## Filter Setting Naming Options

1. ScoreThreshold
2. MinimumSimilarity
3. MaximumDistance
4. Radius

## Considered Options

### 1. Add `ScoreThreshold` setting to `VectorSearchOptions` and ignore if not supported

```csharp
public double? ScoreThreshold { get; init; } = null;
```

If this setting is null, no score filtering will be done.
Setting a default is not practical since semantically similar thresholds have very different values for different distance functions.
If the underlying connector or database doesn't support this setting, it is ignored.

Pros:

- Easy to use
- Possible to switch between different dbs without changing this setting.

Cons:

- Confusing to users if they set the setting for a database that doesn't support this.
- The search resultset may be longer / shorter if switching to a different database when using this setting and the support for the setting changes with the switch.

### 2. Add `ScoreThreshold` setting to `VectorSearchOptions` and filter client side if not supported

```csharp
public double? ScoreThreshold { get; init; } = null;
```

If this setting is null, no score filtering will be done.
Setting a default is not practical since semantically similar thresholds have very different values for different distance functions.
If the underlying database doesn't support this setting, filtering will happen client side.
Client side filtering would be easy, efficient and work with paging, since results are always returned from most similar to least similar with vector saerches.
Once results are reached with a score that is less similar than the threshold, no more results have to be returned.

Pros:

- Easy to use
- Possible to switch between different dbs without changing this setting.

Cons:

- Small numbers of results may be fetched from the server that are filtered on the client.

### 3. Add `ScoreThreshold` setting to `VectorSearchOptions` and throw if not supported

```csharp
public double? ScoreThreshold { get; init; } = null;
```

If this setting is null, no score filtering will be done.
Setting a default is not practical since semantically similar thresholds have very different values for different distance functions.
If the underlying connector or database doesn't support this setting, an exception is thrown at search time.

Pros:

- Easy to use
- Save to start with, since we can go to option 1 later if we want to, i.e. be more restrictive to start and go more permissive.

Cons:

- May need to remove this setting if switching between a db that supports the filter and one that doesn't, breaking the abstraction somewhat.

### 4. Add concrete overloads with `ScoreThreshold` only for vector DB connectors that support this setting

```csharp
public double? ScoreThreshold { get; init; } = null;
```

If this setting is null, no score filtering will be done.
Setting a default is not practical since semantically similar thresholds have very different values for different distance functions.

Pros:

- Only DBs that support this feature has this setting on their concrete implementations.

Cons:

- Harder to to use
- More dev work and maintenance
- Always Need to break out of the abstraction to use this feature
