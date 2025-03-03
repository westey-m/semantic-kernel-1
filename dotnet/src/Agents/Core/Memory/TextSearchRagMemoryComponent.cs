// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Data;

namespace Microsoft.SemanticKernel.Agents.Memory;

public class TextSearchRagMemoryComponent : MemoryComponent
{
    private readonly ITextSearch _textSearch;
    private readonly TextSearchFilter? _filterClause;

    public TextSearchRagMemoryComponent(ITextSearch textSearch, TextSearchFilter? filterClause)
    {
        this._textSearch = textSearch;
        this._filterClause = filterClause;
    }

    /// <inheritdoc/>
    public override async Task<string> OnAIInvocationAsync(ChatMessageContent newMessage, CancellationToken cancellationToken = default)
    {
        if (newMessage.Content == null)
        {
            return string.Empty;
        }

        var searchResult = await this._textSearch.GetTextSearchResultsAsync(
            newMessage.Content,
            new()
            {
                Top = 3,
                Filter = this._filterClause
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var resultItems = await searchResult.Results.ToListAsync(cancellationToken).ConfigureAwait(false);
        var formattedResults = string.Join("\n", resultItems.Select(r =>
        {
            return
            $""""
            Name: {r.Name}
            Link: {r.Link}
            Value: {r.Value}
            """";
        }));

        Console.WriteLine($"- TextSearchRagMemory - Retrieved the following context using RAG:\n{formattedResults}");
        return $"Consider the following additional information when answering the user question:\n {formattedResults}";
    }
}
