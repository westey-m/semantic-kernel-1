// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.SemanticKernel.Agents.Memory;

/// <summary>
/// A memory component that can retrieve, maintain and store user preferences that
/// are learned from the user's interactions with the agent.
/// </summary>
public class UserPreferencesMemory : AgentsMemory
{
    private readonly Kernel _kernel;
    private readonly MemoryDocumentStore? _memoryDocumentStore;
    private string _userPreferences = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserPreferencesMemory"/> class.
    /// </summary>
    /// <param name="kernel">A kernel to use for making chat completion calls.</param>
    /// <param name="userPreferencesStoreName">The service key that the <see cref="MemoryDocumentStore"/> for user preferences is registered under in DI.</param>
    public UserPreferencesMemory(Kernel kernel, string? userPreferencesStoreName = "UserPreferencesStore")
    {
        this._kernel = kernel;
        this.UserPreferencesStoreName = userPreferencesStoreName;
        if (userPreferencesStoreName is not null)
        {
            this._memoryDocumentStore = kernel.Services.GetRequiredKeyedService<MemoryDocumentStore>(userPreferencesStoreName);
        }
    }

    /// <summary>
    /// Gets or sets the service key that the <see cref="MemoryDocumentStore"/> for user preferences is registered under in DI.
    /// </summary>
    public string? UserPreferencesStoreName { get; private set; }

    /// <summary>
    /// Gets or sets the name of the document to use for storing user preferences.
    /// </summary>
    public string UserPreferencesDocumentName { get; init; } = "UserPreferences";

    /// <summary>
    /// Gets or sets the prompt template to use for extracting user preferences and merging them with existing preferences.
    /// </summary>
    public string MaintainencePromptTemplate { get; init; } = """
You are an expert in extracting facts about a user from text and combining these facts with existing facts to output a new list of facts.
Facts are short statements that each contain a single piece of information.

Here are some few shot examples:

Input text: My name is John. I love dogs and cats, but unfortunately I am allergic to cats. I'm not alergic to dogs though. I have a dog called Fido.
Input facts: User name is John. User is alergic to cats.
Output: User name is John. User loves dogs. User loves cats. User is alergic to cats. User is not alergic to dogs. User has a dog. User dog's name is Fido.

Input text: My name is Mary. I like active holidays. I enjoy cycling and hiking.
Input facts: User name is Mary. User dislikes cycling.
Output: User name is Mary. User likes cycling. User likes hiking. User likes active holidays.

Return output for the following inputs like shown in the examples above:

Input text: {{$inputText}}
Input facts: {{$existingPreferences}}
""";

    /// <inheritdoc/>
    public override async Task LoadContextAsync(string? inputText = default, CancellationToken cancellationToken = default)
    {
        this._userPreferences = string.Empty;

        if (this._memoryDocumentStore is not null)
        {
            var memoryText = await this._memoryDocumentStore.GetMemoryAsync("UserPreferences", cancellationToken).ConfigureAwait(false);
            if (memoryText is not null)
            {
                this._userPreferences = memoryText;
            }
        }

        // this._userPreferences = "User prefers an informal tone. User likes puns.";

        Console.WriteLine("- UserPreferencesMemory - Loading user preferences context");
        Console.WriteLine("    " + this._userPreferences);
    }

    /// <inheritdoc/>
    public override async Task SaveContextAsync(ChatHistory currentChatHistory, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("- UserPreferencesMemory - Saving user preferences context");
        Console.WriteLine("    " + this._userPreferences);

        if (this._memoryDocumentStore is not null)
        {
            await this._memoryDocumentStore.SaveMemoryAsync("UserPreferences", this._userPreferences, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public override async Task MaintainContextAsync(ChatMessageContent newMessage, ChatHistory currentChatHistory, CancellationToken cancellationToken = default)
    {
        if (newMessage.Role == AuthorRole.User)
        {
            var result = await this._kernel.InvokePromptAsync(
                this.MaintainencePromptTemplate,
                new KernelArguments() { ["inputText"] = newMessage.Content, ["existingPreferences"] = this._userPreferences },
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var newPreferences = result.ToString();

            Console.WriteLine($"- UserPreferencesMemory - Performed maintainence on user preferences context.\n    Old Context: {this._userPreferences}\n    New Context: {newPreferences}");

            this._userPreferences = newPreferences;
        }
    }

    /// <inheritdoc/>
    public override Task<string> GetRenderedContextAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult("The following list contains facts about hte user:\n" + this._userPreferences);
    }
}
