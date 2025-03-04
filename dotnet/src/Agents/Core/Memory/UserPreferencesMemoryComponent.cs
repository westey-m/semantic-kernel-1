// Copyright (c) Microsoft. All rights reserved.

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel.Agents.Memory;

/// <summary>
/// A memory component that can retrieve, maintain and store user preferences that
/// are learned from the user's interactions with the agent.
/// </summary>
public class UserPreferencesMemoryComponent : MemoryComponent
{
    private readonly Kernel _kernel;
    private readonly MemoryDocumentStore _memoryDocumentStore;
    private string _userPreferences = string.Empty;
    private bool _contextLoaded = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserPreferencesMemoryComponent"/> class.
    /// </summary>
    /// <param name="kernel">A kernel to use for making chat completion calls.</param>
    /// <param name="memoryDocumentStore">The memory document store to retrieve and save memories from and to.</param>
    public UserPreferencesMemoryComponent(Kernel kernel, MemoryDocumentStore memoryDocumentStore)
    {
        this._kernel = kernel;
        this._memoryDocumentStore = memoryDocumentStore;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserPreferencesMemoryComponent"/> class.
    /// </summary>
    /// <param name="kernel">A kernel to use for making chat completion calls.</param>
    /// <param name="userPreferencesStoreName">The service key that the <see cref="MemoryDocumentStore"/> for user preferences is registered under in DI.</param>
    public UserPreferencesMemoryComponent(Kernel kernel, string? userPreferencesStoreName = "UserPreferencesStore")
    {
        this._kernel = kernel;
        this._memoryDocumentStore = new OptionalDocumentStore(kernel, userPreferencesStoreName);
    }

    /// <summary>
    /// Gets or sets the name of the document to use for storing user preferences.
    /// </summary>
    public string UserPreferencesDocumentName { get; init; } = "UserPreferences";

    /// <summary>
    /// Gets or sets the prompt template to use for extracting user preferences and merging them with existing preferences.
    /// </summary>
    public string MaintainencePromptTemplate { get; init; } =
        """
        You are an expert in extracting facts about a user from text and combining these facts with existing facts to output a new list of facts.
        Facts are short statements that each contain a single piece of information.
        Facts should always be about the user and should always be in the present tense.
        Facts should focus on the user's long term preferences and characteristics, not on their short term actions.

        Here are 5 few shot examples:

        EXAMPLES START

        Input text: My name is John. I love dogs and cats, but unfortunately I am allergic to cats. I'm not alergic to dogs though. I have a dog called Fido.
        Input facts: User name is John. User is alergic to cats.
        Output: User name is John. User loves dogs. User loves cats. User is alergic to cats. User is not alergic to dogs. User has a dog. User dog's name is Fido.

        Input text: My name is Mary. I like active holidays. I enjoy cycling and hiking.
        Input facts: User name is Mary. User dislikes cycling.
        Output: User name is Mary. User likes cycling. User likes hiking. User likes active holidays.
        
        Input text: How do I calculate the area of a circle?
        Input facts: 
        Output: 
        
        Input text: What is today's date?
        Input facts: User name is Peter.
        Output: User name is Peter.

        EXAMPLES END
        
        Return output for the following inputs like shown in the examples above:

        Input text: {{$inputText}}
        Input facts: {{$existingPreferences}}
        """;

    /// <inheritdoc/>
    public override async Task OnThreadStartAsync(string threadId, string? inputText = default, CancellationToken cancellationToken = default)
    {
        if (!this._contextLoaded)
        {
            this._userPreferences = string.Empty;

            var memoryText = await this._memoryDocumentStore.GetMemoryAsync("UserPreferences", cancellationToken).ConfigureAwait(false);
            if (memoryText is not null)
            {
                this._userPreferences = memoryText;
            }

            Console.WriteLine("- UserPreferencesMemory - Loading user preferences context"
                + (string.IsNullOrWhiteSpace(this._userPreferences) ? string.Empty : $"\n    {this._userPreferences}"));

            this._contextLoaded = true;
        }
    }

    /// <inheritdoc/>
    public override async Task OnThreadEndAsync(string threadId, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("- UserPreferencesMemory - Saving user preferences context"
            + "\n    " + this._userPreferences);

        await this._memoryDocumentStore.SaveMemoryAsync("UserPreferences", this._userPreferences, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task OnNewMessageAsync(ChatMessageContent newMessage, CancellationToken cancellationToken = default)
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
    public override Task<string> OnAIInvocationAsync(ChatMessageContent newMessage, CancellationToken cancellationToken = default)
    {
        return Task.FromResult("The following list contains facts about the user:\n" + this._userPreferences);
    }

    public override void RegisterPlugins(Kernel kernel)
    {
        base.RegisterPlugins(kernel);
        kernel.Plugins.AddFromObject(this, "UserPreferencesMemory");
    }

    [KernelFunction]
    [Description("Deletes any user preferences stored about the user.")]
    public void ClearUserPreferences()
    {
        this._userPreferences = string.Empty;
        Console.WriteLine("- UserPreferencesMemory - User preferences cleared via plugin call.");
    }
}
