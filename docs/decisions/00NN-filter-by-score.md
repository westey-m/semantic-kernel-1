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

|Feature|Azure AI Search|Weaviate|Redis|Chroma|Pinecone|PostgreSql|Qdrant|Milvus|Elasticsearch|CosmosDB NoSql|MongoDB|
|-|-|-|-|-|-|-|-|-|-|-|-|
|Supports theshold in api|Y (in preview)|Y|Y (but not in combination with KNN)|-|Can't find support in docs|Y|Y|Y (both upper and lower bound)|Y|Seems possible, needs testing|[Supported via hack with limitations](https://www.mongodb.com/community/forums/t/search-for-all-vectors-with-similarity-above-threshold/274637?msockid=3e0a5061bf0868e0138e4508be446965)|
|Threshold naming|[threshold](https://learn.microsoft.com/en-us/azure/search/vector-search-how-to-query?tabs=query-2024-07-01%2Cbuiltin-portal#set-thresholds-to-exclude-low-scoring-results-preview)|max [distance](https://weaviate.io/developers/weaviate/search/similarity#set-a-similarity-threshold)|[radius](https://redis.io/docs/latest/develop/interact/search-and-query/advanced-concepts/vectors/) also see sample [here](https://redis.io/docs/latest/develop/interact/search-and-query/query/vector-search/)|-|-|['distance' in docs, not used in syntax](https://github.com/pgvector/pgvector?tab=readme-ov-file#querying)|[score_threshold](https://api.qdrant.tech/api-reference/search/query-points#request.body.score_threshold)|[radius](https://milvus.io/docs/range-search.md)|minimum [similarity](https://www.elastic.co/guide/en/elasticsearch/reference/current/query-dsl-knn-query.html#knn-query-top-level-parameters)|-|-|

Langchain uses score_threshold for naming, see [here](https://api.python.langchain.com/en/latest/vectorstores/langchain_community.vectorstores.qdrant.Qdrant.html#langchain_community.vectorstores.qdrant.Qdrant.asimilarity_search_by_vector).
However, it doesn't support the parameter consistently, e.g. it's simply missing for [pinecone](https://api.python.langchain.com/en/latest/vectorstores/langchain_pinecone.vectorstores.PineconeVectorStore.html#langchain_pinecone.vectorstores.PineconeVectorStore.asimilarity_search_by_vector), which I don't believe support this feature.

[Cosmos DB NoSQL Query Doc](https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/vector-search#perform-vector-search-with-queries-using-vectordistance)
