---
# These are optional elements. Feel free to remove any of them.
status: {proposed | rejected | accepted | deprecated | … | superseded by [ADR-0001](0001-madr-architecture-decisions.md)}
contact: {person proposing the ADR}
date: {YYYY-MM-DD when the decision was last updated}
deciders: {list everyone involved in the decision}
consulted: {list everyone whose opinions are sought (typically subject-matter experts); and with whom there is a two-way communication}
informed: {list everyone who is kept up-to-date on progress; and with whom there is a one-way communication}
---

# Agents with Memory

## Context and Problem Statement

Today we support multiple agent types that can both be in process as well as remote. Agents can be both stateful and stateless.
We need to support advanced memory capabilities across this range of agent types.

We can add memory capabilities via a simple mechanism of:

1. Inspecting and using messages as they are passed to and from the agent.
1. Passing additional context to the agent per invocation.

A requirement for an agent to be usable with our memory capabilities is therefore the need to accept additional per invocation context.

Where agents are remote/external and stateful, any memory capabilities that we add, will not directly affect the state of the remote agent,
since any memories will be passed as temporary context per invocation.

Each memory capability can be built using a separate component, which has the following characteristics:

1. May store some context that can be provided to the agent per invocation.
1. May inspect messages from the conversation to learn from the conversation and build its context.
1. May register plugins to allow the agent to directly store, retrieve, update or clear memories.

The same memory components can also be used to build a stateful agent, since the same mechanisms of inspecting messages and passing
additional context to (in this case) the LLM, can be used here too.
In addition, building a stateful agent also requires the context that each component may hold to be storable between invocations.
This way the conversation can be resumed from this stored state when an invocation to the conversation is received and suspended at the
end of the invocation, allowing the caller to experience a stateful experience.

### Proposed interface for Memory Components

The types of events that Memory Components require are not unique to memory, and can be used to package up other capabilities too.
The suggestion is therefore to create a more generally named type that can be used for other scenarios as well.

```csharp
public abstract class ChatExtensionComponent
{
    public virtual Task OnThreadStartAsync(string threadId, string? inputText = default, CancellationToken cancellationToken = default);
    public virtual Task OnThreadCheckpointAsync(string threadId, CancellationToken cancellationToken = default);
    public virtual Task OnThreadEndAsync(string threadId, CancellationToken cancellationToken = default);

    public virtual Task OnNewMessageAsync(ChatMessageContent newMessage, CancellationToken cancellationToken = default);
    public abstract Task<string> OnAIInvocationAsync(ChatMessageContent newMessage, CancellationToken cancellationToken = default);

    public virtual void RegisterPlugins(Kernel kernel);
}
```

Alternative names:

- ThreadComponent
- ChatComponent
- MemoryComponent

### Thread Management

Different agent types have different mechanisms for supporting threads and storing conversation history.
E.g. some are stateless and require the caller to manage this, while others are stateful and expose thread mangement capabilities.

We therefore need a way to interact with the different thread management capabilities of different agents via an abstraction.
This abstraction can proxy calls to the thread management capabilities of remote agents, or implement in-memory
chat history management for stateless agents.

Here is a suggested base interface for managing threads.

```csharp
public abstract class ChatThread
{
    public abstract bool HasActiveThread { get; }
    public abstract string? CurrentThreadId { get; }

    public abstract Task<string> StartNewThreadAsync(CancellationToken cancellationToken = default);
    public abstract Task EndThreadAsync(CancellationToken cancellationToken = default);
    public abstract Task<ChatHistory> RetrieveCurrentChatHistoryAsync(CancellationToken cancellationToken = default);
}
```

Alternative names:

- ChatContext

### Combining Threads, Agents and Memory Components

We need the ability to combine Agent, Thread, and memory components.
Remote Agents that expose their own thread management API will require a matching `ChatThread` component to function correctly.
This matching `ChatThread` component would simply proxy to the thread management API of the service.

Stateless agents on the other hand, could be combined with one of multiple availble `ChatThread` components.
You could for example have:

1. A `ChatThread` component that stores state locally in-memory in an SK ChatHistory component and supports suspend/resume.
1. A `ChatThread` component that uploads chat messages into a remote message store that does truncation automatically.

A choice of `ChatThread` components also make sense for when you are building your own stateful agent service.

An agent can also be combined with one or more memory components, depending on the type of memory capabilities required.
**There is a close relationship between the instances of the thread and memory components associated with an agent.**
**This is regardless of whether the thread ultimately lives in a remote agent service or is managed locally.**

A specific thread is going to contain messages from the user(s) and agent(s) that are participating in the thread.
These messages are typically considered private to the participants of the thread.
Similarly, most memories that are derived from this thread, e.g. chat summaries or user preferences, would be considered
private too.
The context stored in each memory component while a thread is active, is private to the thread itself.
A memory component may however store memories to long term storage which could allow sharing of memories between
threads where it is safe to do so.

Here are some sample scenarios.

#### ChatCompletionAgent

ChatCompletionAgent is stateless and designed for local in-memory usage.
To have it participate in a conversation you require a `ChatThread` component that can manage its chat history.
You may also add a memory component to summarize the conversation and store it in a vector database, plus
retrieve previous conversations that are interesting.
You may also add a second memory component that learns about user preferences, makes them available in context and saves them across conversations.

- ChatCompletionAgent
- ChatHistoryChatThread
- ConversationSummaryStorageMemoryComponent
- UserPreferencesMemoryComponent

#### OpenAIAssistantAgent

OpenAIAssistantAgent is stateful and remote.
To have it participate in a conversation you require a `ChatThread` component that can proxy calls to its own thread management.
Let's say the agent can also remember details from previous conversations, you would not need a component to do any conversation summarization, storage or retrieval.
You may however add a memory component that learns about user preferences, makes them available in context and saves them across conversations.

- OpenAIAssistantAgent
- OpenAIAssistantChatThread
- UserPreferencesMemoryComponent

## Starting a conversation with a new thread

Agent class instance is stateful and manages thread and memory inside.

```csharp
var agent = new MyAgent()
    {
        Name = "CreditorsAgent",
        Instructions = "You are able to help pay creditors and ensure that invoices are processed and receipts are consolidated.",
        Kernel = kernel,
        Extensions = [new UserPreferencesMemoryComponent(kernel)]
    };

var response = await agent.InvokeAsync("Has invoice 33-22-6324 from Fabrikam been paid yet?");
var response = await agent.InvokeAsync("And invoice 33-22-6325?");

Console.WriteLine(agent.ThreadId);
await agent.EndThread();
```

Agent class is stateless and thread and memory is managed separately.
Allows you to pass thread specific config to a new thread when creating it.
Allows you to fork threads, if each response contains the newly forked thread.
Potentially complicates scenario where multiple agents need to converse together with the same thread and you don't want them to share memory components.

```csharp
var agent = new MyAgent()
    {
        Name = "CreditorsAgent",
        Instructions = "You are able to help pay creditors and ensure that invoices are processed and receipts are consolidated.",
        Kernel = kernel
    }
var thread = new MyAgentChatThread()
{
    Extensions = [new UserPreferencesMemoryComponent(kernel)]
};
await thread.StartThread();

var response = await agent.InvokeAsync("Has invoice 33-22-6324 from Fabrikam been paid yet?", thread, memoryManager);
var response = await agent.InvokeAsync("And invoice 33-22-6325?", thread, memoryManager);

Console.WriteLine(thread.ThreadId);
await thread.EndThread();
```

Agent class is stateless and thread and memory is managed separately, with auto thread creation.

```csharp
var agent = new MyAgent()
    {
        Name = "CreditorsAgent",
        Instructions = "You are able to help pay creditors and ensure that invoices are processed and receipts are consolidated.",
        Kernel = kernel
    }

var response = await agent.InvokeAsync("Has invoice 33-22-6324 from Fabrikam been paid yet?", [new UserPreferencesMemoryComponent(kernel)]);
var response = await agent.InvokeAsync("And invoice 33-22-6325?", response.Thread);

Console.WriteLine(response.Thread.ThreadId);
await response.Thread.EndThread();
```

## Resuming a conversation on an existing thread

Resuming previous thread with stateful agent class.

```csharp
var agent = new MyAgent()
    {
        Name = "CreditorsAgent",
        Instructions = "You are able to help pay creditors and ensure that invoices are processed and receipts are consolidated.",
        Kernel = kernel,
        ThreadId = "12345",
        Extensions = [new UserPreferencesMemoryComponent(kernel)]
    };
```

Resuming previous thread when agent class is stateless and thread and memory is managed separately.

```csharp
var agent = new MyAgent()
    {
        Name = "CreditorsAgent",
        Instructions = "You are able to help pay creditors and ensure that invoices are processed and receipts are consolidated.",
        Kernel = kernel
    }
var thread = new MyAgentChatThread("12345");
```

## Multi-agent conversation using stateful pattern

```csharp
var agent1 = new MyAgent()
    {
        Name = "Copywriter",
        Instructions = "You are an experienced copywriter.",
        Kernel = kernel,
        Extensions = [new UserPreferencesMemoryComponent(kernel)]
    };

var agent2 = new MyAgent()
    {
        Name = "Editor",
        Instructions = "You are an experienced editor.",
        Kernel = kernel
    };

var groupChat = new GroupChat(agent1, agent2)
{
    Extensions = [new ChatHistorySummarizedStorage(kernel)]
};

var response = await groupChat.InvokeAsync("Write a funny marketing slogan for decorative lamps made with tree bark?");

Console.WriteLine(agent1.ThreadId);
Console.WriteLine(agent2.ThreadId);
Console.WriteLine(groupChat.ThreadId);
// End all threads?
await groupChat.EndThread();
```

## Comparing current agent invocation

Using the chat completion agent

```csharp
ChatCompletionAgent agent =
    new()
    {
        Instructions = "You are a friendly assistant",
        Name = "FriendlyAssistant",
        Kernel = kernel,
    };
ChatHistory chat = [new ChatMessageContent(AuthorRole.User, "What is the capital of France")];
await agent.InvokeAsync(chat);
```

Using the Azure AI Agent

```csharp
var azureAIClient = AzureAIAgent.CreateAzureAIClient(TestConfiguration.AzureAI.ConnectionString, new AzureCliCredential());
var azureAIAgentsClient = azureAIClient.GetAgentsClient();
var definition = await azureAIAgentsClient.CreateAgentAsync("gpt-4o", "FriendlyAssistant", "FriendlyAssistant", "You are a friendly assistant");
var thread = new AzureAIAgent(definition, azureAIAgentsClient) { Kernel = kernel };

var createThreadResponse = await azureAIAgentsClient.CreateThreadAsync();

agent.AddChatMessageAsync(thread.Id, new ChatMessageContent(AuthorRole.User, "What is the capital of France"));
await agent.InvokeAsync(thread.Id);
```

Using the Azure OpenAI Assistant Agent

```csharp
var client = OpenAIAssistantAgent.CreateAzureOpenAIClient(new AzureCliCredential(), new Uri(this.Endpoint!));
var assistantClient = client.GetAssistantClient();
Assistant assistant =
    await assistantClient.CreateAssistantAsync(
        this.Model,
        name: "FriendlyAssistant",
        instructions: "You are a friendly assistant");
OpenAIAssistantAgent agent = new(assistant, assistantClient) { Kernel = kernel };

var createThreadResponse = await assistantClient.CreateThreadAsync();

await agent.AddChatMessageAsync(createThreadResponse.Value.Id, new ChatMessageContent(AuthorRole.User, "What is the capital of France"));
await agent.InvokeAsync(createThreadResponse.Value.Id);
```

Using the Bedrock Agent

```csharp
var client = new AmazonBedrockAgentClient();
var agentModel = await this.Client.CreateAndPrepareAgentAsync(new()
{
    AgentName = "FriendlyAssistant",
    Description = "Friendly Assistant",
    Instruction = "You are a friendly assistant",
    AgentResourceRoleArn = TestConfiguration.BedrockAgent.AgentResourceRoleArn,
    FoundationModel = TestConfiguration.BedrockAgent.FoundationModel,
});
var agent = new BedrockAgent(agentModel, this.Client);
agent.InvokeAsync(BedrockAgent.CreateSessionId(), "What is the capital of France");
```
