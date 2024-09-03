// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.Data;

namespace Memory;

public class VectorSearch_Options
{
    public async Task VectorSearchAsync(IVectorSearch<Glossary> vectorSearch)
    {
        var searchEmbedding = new ReadOnlyMemory<float>(new float[1536]);

        // Vector search.
        var searchResults = vectorSearch.SearchAsync(VectorSearchQuery.CreateQuery(searchEmbedding));
        searchResults = vectorSearch.SearchAsync(searchEmbedding); // Extension method.

        // Vector search with specific vector field.
        searchResults = vectorSearch.SearchAsync(VectorSearchQuery.CreateQuery(searchEmbedding, new() { VectorFieldName = nameof(Glossary.DefinitionEmbedding) }));
        searchResults = vectorSearch.SearchAsync(searchEmbedding, new() { VectorFieldName = nameof(Glossary.DefinitionEmbedding) }); // Extension method.

        // Text vector search.
        searchResults = vectorSearch.SearchAsync(VectorSearchQuery.CreateQuery("What does Semantic Kernel mean?"));
        searchResults = vectorSearch.SearchAsync("What does Semantic Kernel mean?"); // Extension method.

        // Text vector search with specific vector field.
        searchResults = vectorSearch.SearchAsync(VectorSearchQuery.CreateQuery("What does Semantic Kernel mean?", new() { VectorFieldName = nameof(Glossary.DefinitionEmbedding) }));
        searchResults = vectorSearch.SearchAsync("What does Semantic Kernel mean?", new() { VectorFieldName = nameof(Glossary.DefinitionEmbedding) }); // Extension method.

        // Hybrid vector search.
        searchResults = vectorSearch.SearchAsync(VectorSearchQuery.CreateHybridQuery(searchEmbedding, "What does Semantic Kernel mean?", new() { HybridFieldName = nameof(Glossary.Definition) }));
        searchResults = vectorSearch.HybridVectorizedTextSearchAsync(searchEmbedding, "What does Semantic Kernel mean?", new() { HybridFieldName = nameof(Glossary.Definition) }); // Extension method.

        // Hybrid text vector search with field names specified for both vector and keyword search.
        searchResults = vectorSearch.SearchAsync(VectorSearchQuery.CreateHybridQuery("What does Semantic Kernel mean?", new() { VectorFieldName = nameof(Glossary.DefinitionEmbedding), HybridFieldName = nameof(Glossary.Definition) }));
        searchResults = vectorSearch.HybridVectorizableTextSearchAsync("What does Semantic Kernel mean?", new() { VectorFieldName = nameof(Glossary.DefinitionEmbedding), HybridFieldName = nameof(Glossary.Definition) }); // Extension method.

        // Vector search with options.
        var filter = new VectorSearchFilter().Equality(nameof(Glossary.Category), "Core Definitions");
        searchResults = vectorSearch.SearchAsync(
            VectorSearchQuery.CreateQuery(
                searchEmbedding,
                new()
                {
                    VectorSearchFilter = filter,
                    VectorFieldName = nameof(Glossary.DefinitionEmbedding)
                }));

        // Hybrid vector search with options.
        filter = new VectorSearchFilter().Equality(nameof(Glossary.Category), "Core Definitions");
        searchResults = vectorSearch.SearchAsync(
            VectorSearchQuery.CreateHybridQuery(
                searchEmbedding,
                "What does Semantic Kernel mean?",
                new()
                {
                    VectorSearchFilter = filter,
                    HybridFieldName = nameof(Glossary.Definition),
                    VectorFieldName = nameof(Glossary.DefinitionEmbedding)
                }));
    }

    public sealed class Glossary
    {
        [VectorStoreRecordKey]
        public ulong Key { get; set; }

        [VectorStoreRecordData]
        public string Category { get; set; }

        [VectorStoreRecordData]
        public string Term { get; set; }

        [VectorStoreRecordData]
        public string Definition { get; set; }

        [VectorStoreRecordVector(1536)]
        public ReadOnlyMemory<float> DefinitionEmbedding { get; set; }
    }
}
