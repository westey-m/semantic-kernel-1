// Copyright (c) Microsoft. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Agents.Memory;
using Microsoft.SemanticKernel.Agents.OpenAI.Internal;
using OpenAI.Assistants;

namespace Microsoft.SemanticKernel.Agents.OpenAI;

public class OpenAIAssistantThreadMemoryComponent : MemoryComponent
{
    private bool _threadCreated = false;
    private string _threadId = string.Empty;
    private readonly AssistantClient _client;
    private readonly bool _deleteThreadOnSave;

    public OpenAIAssistantThreadMemoryComponent(AssistantClient client, bool deleteThreadOnSave = true)
    {
        this._client = client;
        this._deleteThreadOnSave = deleteThreadOnSave;
    }

    public string ThreadId => this._threadId;

    public override async Task LoadContextAsync(string? inputText = null, CancellationToken cancellationToken = default)
    {
        await this.CreateThreadIfNeededAsync(cancellationToken).ConfigureAwait(false);
    }

    public override async Task MaintainContextAsync(ChatMessageContent newMessage, CancellationToken cancellationToken = default)
    {
        await this.CreateThreadIfNeededAsync(cancellationToken).ConfigureAwait(false);
        await AssistantThreadActions.CreateMessageAsync(this._client, this._threadId, newMessage, cancellationToken).ConfigureAwait(false);
    }

    public override async Task SaveContextAsync(CancellationToken cancellationToken = default)
    {
        if (this._threadCreated && this._deleteThreadOnSave)
        {
            await this._client.DeleteThreadAsync(this._threadId, cancellationToken).ConfigureAwait(false);
        }
    }

    public override Task<string> GetRenderedContextAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(string.Empty);
    }

    private async Task CreateThreadIfNeededAsync(CancellationToken cancellationToken)
    {
        if (this._threadCreated)
        {
            return;
        }

        var assitantThreadResponse = await this._client.CreateThreadAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        this._threadId = assitantThreadResponse.Value.Id;
        this._threadCreated = true;
    }
}
