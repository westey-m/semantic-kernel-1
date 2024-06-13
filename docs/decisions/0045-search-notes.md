### Vector / Hybrid / Filtered Search

We need to support vector search with filtering and combined with full text search.
This should use a layered approach where additional complexity is layered on top of each other, since not all databases or data sources
support all types of filtering.

Assumptions:
1. While it is possible to search multiple vectors in one go on some databases, we do not necessarily need to support it, since it is possible to do separate searches too.
2. While it is possible to do a text search on multiple fields and a vector search on another unrelated at the same time, our main use case is hybrid search where we do both on
the two related data and vector fields, so that's the main use case being targeted here.
3. Considering the high degree of variability in text search capabilities, we'll have to be opinionated in what we support for keyword search for hybrid search, e.g. supporting just a list of keywords.

Layers:
1. TextSearchService: Supports searching a source using text only, and can be used across any source including search engines.
2. FilteredTextSearchService: Supports simple configurable filtering in additional to a search using a piece of text.
3. FileredHybridSearchService: Supports filtering, vector based text search and full text search.

```cs
class TextSearchResult {
    public string Text {get; init; };
    public string Source {get; init; };
}
// Text only input.
interface ITextSearch {
    IAsyncEnumerable<SearchResult> Search(string text, string? collectionName);
}
// Text and filter input.
interface IFilteredTextSearch {
    IAsyncEnumerable<SearchResult> Search(string text, string filter, string? collectionName);  // Filter using odata string.
    IAsyncEnumerable<SearchResult> Search(string text, IEnumerable<KeyValuePair<string, string>> filter, string? collectionName);  // Filter using key value pairs.
}
// Text, filter and FTS keyword input.
interface IHybridFilteredTextSearch {
    IAsyncEnumerable<SearchResult> Search(string text, IEnumerable<string> keywords, string filter, string? collectionName);  // Filter using odata string.
    IAsyncEnumerable<SearchResult> Search(string text, IEnumerable<string> keywords, IEnumerable<KeyValuePair<string, string>> filter, string? collectionName);  // Filter using key value pairs.
}
// Filter only input.
interface IFilteredSearch {
    IAsyncEnumerable<SearchResult> Search(string filter, string? collectionName); // Filter using odata string.
    IAsyncEnumerable<SearchResult> Search(IEnumerable<KeyValuePair<string, string>> filter, string? collectionName);  // Filter using key value pairs.
}

// For each of the above, we should also support a generic version that returns the data model matching storage, e.g.
interface ITextSearch<TDataType> {
    IAsyncEnumerable<TDataType> Search(string text, string? collectionName);
}

// Text searches using bing.
class BingTextSearch: ITextSearch;
// Searches in Azure AI Search, and supports all text interfaces for doing searches.
class AzureAISearchHybridFilteredTextSearch(
    SearchIndexClient searchIndexClient,
    string targetVectorField,
    string targetTextSearchField,
    string? defaultCollectionName,
    string? baseFilter): ITextSearch, IFilteredSearch, IFilteredTextSearch, IHybridFilteredTextSearch;
// How / what do we serialize here? Do we allow a specific field to be picked, just convert to json, or allow a custom mapping method (what is the input to that?).
    serializationType = Json | TextField | Mapper
    string resultTextField // Pick a text field to return.
    Func<string, JsonObject> // Custom mapper from json to string.
    Func<string, TDataModel> // Custom mapper from data model to string.

class AzureAISearchHybridFilteredTextSearch<TDataModel>(
    SearchIndexClient searchIndexClient,
    string targetVectorField,
    string targetTextSearchField,
    string? defaultCollectionName,
    string? baseFilter): ITextSearch<TDataModel>, IFilteredSearch<TDataModel>, IFilteredTextSearch<TDataModel>, IHybridFilteredTextSearch<TDataModel>;

```

```json
{
    "textSearch": [
        {
            "field": "Description",
            "fullTextQuery": "blue",
            "vector": [1, 2, 3, 4]
        }
    ],
    "filter": "category eq 'foo'"
}
```


### Simple RAG using .net 8 Keyed Services

Shows a simple example of adding a prompt filter to retrieve data related to the user's prompt
and add it to the prompt before sending it to the LLM.

```cs
// Basic RAG implementation that just takes the user input and searches for related data, which is added to the prompt.
class RAGPromptFilter(ITextSearchService textSearchService): IPromptRenderFilter
{
    public async Task OnPromptRenderAsync(PromptRenderContext context, Func<PromptRenderContext, Task> next)
    {
        await next(context);
        var prompt = context.RenderedPrompt!;

        // Search for information relating to the user's prompt to pass to the LLM.
        var searchResult = await textSearchService.SearchAsync(
            prompt,
            limit: 1).FirstOrDefaultAsync();
        // Add it to the prompt if we found anything.
        if (searchResult is not null)
        {
            context.RenderedPrompt = $"<message role=\"system\">Use the following information to answer the user: {searchResult.Text}</message>" + context.RenderedPrompt;
        }
    }
}

// Variant 1: AzureAISearchFilteredSearchService allows any type of data model to be returned, but FilteredVectorTextSearchService expects Json as input
// so we construct AzureAISearchFilteredSearchService with a json model and just use serialized json as the LLM input.
class AzureAISearchFilteredSearchService<TDataModel>(ITextEmbeddingService textEmbeddingService, SearchIndexClient searchIndexClient): IFilteredVectorSearchService<TDataModel>;
class FilteredVectorTextSearchService(IFilteredVectorSearchService<JsonNode> filteredVectorSearchService, FilterConfig filterConfig): ITextSearchService;

// Variant 2: AzureAISearchFilteredSearchService allows any type of data model to be returned, and we can provide our custom data model there too, i.e. BookingFaq, but we want
// a custom serialization method when serializing the search output to a string for LLM input, so we create the FilteredVectorTextSearchService in a way that it expects string
// output from the IFilteredVectorSearchService and we add a IFilteredVectorSearchService decorator that can do the custom conversion.
// Construction looks similar to this:
// new FilteredVectorTextSearchService(
//      new SerializingFiteredVectorSearchService(
//          new AzureAISearchFilteredSearchService<BookingFaq>(sp.GetRequiredService<ITextEmbeddingService>(), new SearchIndexClient(new Uri(azureAISearchEndpoint), apiKey))
//          converter),
//      new FilterConfig { VectorSearchFieldName = vectorSearchFieldName });
class AzureAISearchFilteredSearchService<TDataModel>(ITextEmbeddingService textEmbeddingService, SearchIndexClient searchIndexClient): IFilteredVectorSearchService<TDataModel>;
class SerializingFiteredVectorSearchService(IFilteredVectorSearchService<TDataModel> fitleredVectorSearchService, Func<BookingFaq, string> converter): IFilteredVectorSearchService<string>;
class FilteredVectorTextSearchService(IFilteredVectorSearchService<string> filteredVectorSearchService, FilterConfig filterConfig): ITextSearchService;

builder
    .AddAzureOpenAITextEmbeddingGeneration(textEmbeddingDeploymentName, azureAIEndpoint, apiKey)
    // Variant 1
    // Add keyed azure ai text search implementation, providing the endpoint information and the field that needs to be searched.
    // Will register a factory for FilteredVectorTextSearchService that takes a AzureAISearchFilteredSearchService instance and creates a FilterConfig that targets the given vectorSearchField.
    .AddAzureAITextSearchKeyedTransient(key: "BookingSiteFAQSearch", azureAISearchEndpoint, apiKey, vectorSearchFieldName: "faqTextVector")
    
    // Variant 2
    // Same as variant 1, but with converter added.
    .AddAzureAITextSearchKeyedTransient(key: "BookingSiteFAQSearch", azureAISearchEndpoint, apiKey, vectorSearchFieldName: "faqTextVector", bookingFaqToStringConverter);

builder.Services.AddTransient<IPromptRenderFilter>(sp => {
    return new RAGPromptFilter(sp.GetKeyedService<ITextSearchService>("BookingSiteFAQSearch"));
```

### Native Client Injection via DI Framework using .net 8 Keyed Services

```cs
builder
    .AddKeyedTransient<SearchIndexClient>("cacheSearchIndexClient", sp => { return new SearchIndexClient(new Uri(azureAISearchEndpoint), new AzureKeyCredential(apiKey); })

    // Collection and record registration with config or custom create implementation.
    .AddAzureAISearchStorageKeyedTransient<CacheEntryModel>("Cache", searchIndexClientKey: "cacheSearchIndexClient", createConfiguration)
    .AddAzureAISearchStorageKeyedTransient<CacheEntryModel>("Cache", searchIndexClientKey: "cacheSearchIndexClient", sp => new CacheCreate(...));
```

### RAG Plugin

Shows an example where we create a plugin to allow the LLM to retrieve additional data.

```cs

const string TextSearchFunctionConfig = """
    name: SearchFAQ
    description: Retrieves frequently asked questions and answers related to a user query.
    input_variables:
        - name: text
        description: The user query to find related questions and answers for.
        is_required: true
    output_variable:
        description: String containing questions and answers related to the user query.
    """;
builder
    .ImportPluginFromTextSearchDescription(
        FunctionConfig,
        (sp) => { return sp.GetKeyedService<ITextSearchService>("Cache"); });

const string FilteredTextSearchFunctionConfig = """
    name: SearchHotels
    description: Retrieves hotels that match the provided user descriptions.
    input_variables:
        - name: text
        description: A description of the type of hotel the user wants.
        is_required: true
        - name: hotelCategory
        description: Category of hotel to filter to. Allowed values: 'boutique', 'corporate', 'luxury', 'resort'.
        is_required: false
    output_variable:
        description: String containing a list of hotels matching the given description.
    """;
builder
    .ImportPluginFromSearchDescription(
        FunctionConfig,
        (sp) => { return sp.GetKeyedService<IFilteredTextSearchService>("Hotels"); });

const string FilteredTextSearchFunctionConfig = """
    name: BingSearch
    description: Searches bing for the provided text.
    input_variables:
        - name: text
        description: The text to search bing for.
        is_required: true
        - name: site
        description: The base website url to filter results to.
        is_required: false
    output_variable:
        description: String containing the bing search results for the given text.
    """;
builder
    .ImportPluginFromSearchDescription(
        FunctionConfig,
        (sp) => { return new BingTextSearchService(); });

```
