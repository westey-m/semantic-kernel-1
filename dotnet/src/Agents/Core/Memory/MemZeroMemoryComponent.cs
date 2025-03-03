// Copyright (c) Microsoft. All rights reserved.

using System;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel.Agents.Memory;

public class MemZeroMemoryComponent : MemoryComponent
{
    private static readonly Uri s_searchUri = new("/search", UriKind.Relative);
    private static readonly Uri s_createMemoryUri = new("/memories", UriKind.Relative);

    private readonly string? _agentId;
    private readonly string? _threadId;
    private readonly string? _userId;
    private readonly HttpClient _httpClient;

    private bool _contextLoaded = false;
    private string _userPreferences = string.Empty;

    public MemZeroMemoryComponent(HttpClient httpClient, string? agentId = default, string? threadId = default, string? userId = default)
    {
        this._agentId = agentId;
        this._threadId = threadId;
        this._userId = userId;
        this._httpClient = httpClient;
    }

    /// <inheritdoc/>
    public override async Task OnThreadStartAsync(string? inputText = default, CancellationToken cancellationToken = default)
    {
        if (!this._contextLoaded)
        {
            var searchRequest = new SearchRequest
            {
                AgentId = this._agentId,
                RunId = this._threadId,
                UserId = this._userId,
                Query = inputText ?? string.Empty
            };
            var responseItems = await this.SearchAsync(searchRequest).ConfigureAwait(false);
            this._userPreferences = string.Join("\n", responseItems);
            this._contextLoaded = true;

            Console.WriteLine("- MemZeroMemory - Loading user preferences context"
                + (string.IsNullOrWhiteSpace(this._userPreferences) ? string.Empty : $"\n    {this._userPreferences}"));
        }
    }

    /// <inheritdoc/>
    public override async Task OnNewMessageAsync(ChatMessageContent newMessage, CancellationToken cancellationToken = default)
    {
        if (newMessage.Role == AuthorRole.User)
        {
            await this.CreateMemoryAsync(
                new CreateMemoryRequest()
                {
                    AgentId = this._agentId,
                    RunId = this._threadId,
                    UserId = this._userId,
                    Messages = new[]
                    {
                        new CreateMemoryMemory
                        {
                            Content = newMessage.Content ?? string.Empty,
                            Role = newMessage.Role.Label
                        }
                    }
                }).ConfigureAwait(false);

            Console.WriteLine($"- MemZeroMemory - Updated Mem0 with new message");
        }
    }

    /// <inheritdoc/>
    public override Task<string> OnAIInvocationAsync(ChatMessageContent newMessage, CancellationToken cancellationToken = default)
    {
        return Task.FromResult("The following list contains facts about the user:\n" + this._userPreferences);
    }

    public override void RegisterPlugins(Kernel kernel)
    {
        base.RegisterPlugins(kernel);
        kernel.Plugins.AddFromObject(this, "MemZeroMemory");
    }

    [KernelFunction]
    [Description("Deletes any user preferences stored about the user.")]
    public async Task ClearUserPreferencesAsync()
    {
        await this.ClearMemoryAsync().ConfigureAwait(false);
        Console.WriteLine("- MemZeroMemory - User preferences cleared via plugin call.");
    }

    private async Task CreateMemoryAsync(CreateMemoryRequest createMemoryRequest)
    {
        using var content = new StringContent(JsonSerializer.Serialize(createMemoryRequest), Encoding.UTF8, "application/json");
        var responseMessage = await this._httpClient.PostAsync(s_createMemoryUri, content).ConfigureAwait(false);
        responseMessage.EnsureSuccessStatusCode();
    }

    private async Task<string[]> SearchAsync(SearchRequest searchRequest)
    {
        using var content = new StringContent(JsonSerializer.Serialize(searchRequest), Encoding.UTF8, "application/json");
        var responseMessage = await this._httpClient.PostAsync(s_searchUri, content).ConfigureAwait(false);
        responseMessage.EnsureSuccessStatusCode();
        var response = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
        var searchResponseItems = JsonSerializer.Deserialize<SearchResponseItem[]>(response);
        return searchResponseItems?.Select(item => item.Memory).ToArray() ?? Array.Empty<string>();
    }

    private async Task ClearMemoryAsync()
    {
        try
        {
            var querystringParams = new string?[3] { this._userId, this._agentId, this._threadId }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select((param, index) => $"param{index}={param}");
            var queryString = string.Join("&", querystringParams);
            var clearMemoryUrl = new Uri($"/memories?{queryString}", UriKind.Relative);

            var responseMessage = await this._httpClient.DeleteAsync(clearMemoryUrl).ConfigureAwait(false);
            responseMessage.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"- MemZeroMemory - Error clearing memory: {ex.Message}");
        }
    }

    private class CreateMemoryRequest
    {
        [JsonPropertyName("agent_id")]
        public string AgentId { get; set; } = string.Empty;
        [JsonPropertyName("run_id")]
        public string RunId { get; set; } = string.Empty;
        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = string.Empty;
        [JsonPropertyName("messages")]
        public CreateMemoryMemory[] Messages { get; set; } = [];
    }

    private class CreateMemoryMemory
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;
    }

    private class SearchRequest
    {
        [JsonPropertyName("agent_id")]
        public string? AgentId { get; set; } = null;
        [JsonPropertyName("run_id")]
        public string? RunId { get; set; } = null;
        [JsonPropertyName("user_id")]
        public string? UserId { get; set; } = null;
        [JsonPropertyName("query")]
        public string Query { get; set; } = string.Empty;
    }

    private class SearchResponseItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        [JsonPropertyName("memory")]
        public string Memory { get; set; } = string.Empty;
        [JsonPropertyName("hash")]
        public string Hash { get; set; } = string.Empty;
        [JsonPropertyName("metadata")]
        public object? Metadata { get; set; }
        [JsonPropertyName("score")]
        public double Score { get; set; }
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
        [JsonPropertyName("updated_at")]
        public DateTime? UpdatedAt { get; set; }
        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = string.Empty;
        [JsonPropertyName("agent_id")]
        public string AgentId { get; set; } = string.Empty;
        [JsonPropertyName("run_id")]
        public string RunId { get; set; } = string.Empty;
    }
}
