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

## Multi-agent conversations / processes

Currently different types of multi-agent systems exist.

1. A bag of agents that communicate via chat history with an orchestrator that determines who's next.
1. A defined process containing agents that communicate via and output from one passed as input to the next.
    1. The OpenAI SDK Handoffs support passing a list of events that happened previously, including previous handoffs and agent outputs. This looks to be via chat history as well, where handoffs for example are represented as tool calls.

From an usage perspective the 2nd looks like a very capable single agent, but the first can allow for users
to have visibility of the fact that multiple agents are participating in a conversation with them.

With the first approach, we need the ability to replicate messages from both users and agents to all agents, without it necessarily being that agent's turn to respond.

With the second approach, we need the ability to pass the history of the process up to that point to the next agent in the process.
This means that for an agent to be able to participate in such a process it needs the ability to take an arbitrary chat history as input.

## Invoke Response Type Options

<table>
<tr>
<th>Option</th>
<th>Notes</th>
<th>Sample</th>
</tr>

<tr>
<td>Custom Async Enumerable</td>
<td>
<p>Adds a `Thread` property onto the IAsyncEnumerable.</p>
<p>Allows responses to be enumerated Asynchronously.</p>
<p>Requires awaiting the response and then awaiting the enumerable and thread.</p>
</td>
<td>

```csharp
var responses = await agent.InvokeAsync(
    new ChatMessageContent(AuthorRole.User, "Fortune favors the bold."),
    thread);
var thread = await responses.GetThreadAsync();
var responseItems = await responses.ToListAsync();
```

</td>
</tr>

<tr>
<td>Custom Enumerable</td>
<td>
<p>Adds a `Thread` property onto the IEnumerable.</p>
<p>No asynchronous enumeration.</p>
<p>Only await the enumerable.</p>
</td>
<td>

```csharp
var responses = await agent.InvokeAsync(
    new ChatMessageContent(AuthorRole.User, "Fortune favors the bold."),
    thread);
var thread = responses.Thread;
var responsItems = responses;
```

</td>
</tr>

<tr>
<td>Container class with Enumerable and Thread</td>
<td>
<p>No asynchronous enumeration.</p>
<p>Only await the container.</p>
</td>
<td>

```csharp
var responses = await agent.InvokeAsync(
    new ChatMessageContent(AuthorRole.User, "Fortune favors the bold."),
    thread);
var thread = responses.Thread;
var responseItems = responses.Results;
```

</td>
</tr>

<tr>
<td>IAsyncEnumerable with Container Class elements</td>
<td>
<p>Return an IAsyncEnumerable which contains Container objects around ChatMessageContent and Thread</p>
<p>Can include the thread on the last element</p>
<p>Only await the IAsyncEnumerable.</p>
<p>Also works well with streaming.</p>
</td>
<td>

```csharp
var responses = agent.InvokeAsync(
    new ChatMessageContent(AuthorRole.User, "Fortune favors the bold."),
    thread);
await foreach (var response in responses)
{
    var thread = response.Thread;
    var chatMessageContent = response.ChatMessageContent;
}
```

</td>
</tr>

<tr>
<td>IAsyncEnumerable with elements Inheriting from ChatMessageContent</td>
<td>
<p>Return an IAsyncEnumerable which contains ChatMessageContent objects that have been extended to contain Thread</p>
<p>Can include the thread on the last element</p>
<p>Only await the IAsyncEnumerable.</p>
<p>Also works well with streaming.</p>
</td>
<td>

```csharp
var responses = agent.InvokeAsync(
    new ChatMessageContent(AuthorRole.User, "Fortune favors the bold."),
    thread);
await foreach (var response in responses)
{
    var thread = response.Thread;
    var chatMessageContent = response;
}
```

</td>
</tr>
</table>

## Composition comparison

<table>
<tr>
<th>Feature</th>
<th>Agent contains Thread + Memory<br/>(Higher Level abstraction)</th>
<th>Thread contains Memory and passed on invocation<br/>(Lower Level abstraction)</th>
</tr>
<tr>
<td>Agent Invocation Signature</td>
<td>

```csharp
public Task InvokeAsync(
    ChatMessageContent? chatMessageContent = default,
    KernelArguments? arguments = null,
    Kernel? kernel = null,
    string? additionalInstructions = null,
    CancellationToken cancellationToken? = default);
```

</td>
<td>

```csharp
public Task<InvokeResponse> InvokeAsync(
    ChatMessageContent? chatMessageContent = default,
    ChatThread? thread = default,
    KernelArguments? arguments = null,
    Kernel? kernel = null,
    string? additionalInstructions = null,
    CancellationToken cancellationToken? = default)
```

</td>
</tr>
<tr>
<td>Agent Invocation Sample</td>
<td>

```csharp
agent.InvokeAsync("Sample Prompt");
```

</td>
<td>

```csharp
// Create new thread:
agent.InvokeAsync("Sample Prompt")
// Continue existing thread:
agent.InvokeAsync("Sample Prompt", thread)
```

</td>

</tr>
<tr>
<td>Supplying Memory Components</td>
<td>

Passed to `Agent` constructor

</td>
<td>

Passed to `Thread` constructor.

</td>
</tr>

<tr>
<td>Supplying Memory Components Sample</td>
<td>

```csharp
var agent = new MyAgent()
{
    Name = "SampleAgent",
    Instructions = "Sample Instructions.",
    Kernel = kernel,
    Extensions = [new UserPreferencesMemoryComponent(kernel)]
};
```

</td>
<td>

```csharp
var thread = new MyAgentChatThread()
{
    Extensions = [new UserPreferencesMemoryComponent(kernel)]
};
var response = await agent.InvokeAsync(
    "Sample Prompt",
    thread);
```

</td>

<tr>
<td>Thread management</td>
<td>

`Agent` owns `Thread` and maintains single thread by default.

</td>
<td>

Developer owns `Thread` and each invocation generates updated `Thread` that needs to be used in new invocation to avoid forking.

</td>
</tr>

<tr>
<td>Thread forking</td>
<td>

Clone `Agent` instance.

```csharp
agent.InvokeAsync("Tell me a joke");
var forkedAgent = new ResponsesAgent(agent);

agent.InvokeAsync("Tell me a different joke");
forkedAgent.InvokeAsync("Tell me a different joke")`;
```

</td>
<td>

Use same `Thread` instance twice.

```csharp
var response = agent.InvokeAsync("Tell me a joke");
agent.InvokeAsync("Tell me a different joke", response.Thread);
agent.InvokeAsync("Tell me a different joke", response.Thread);
```

</td>
</tr>

<tr>
<td>Thread specific config</td>
<td>

Construct thread manually with settings before passing to agent.

```csharp
var agent = new MyAgent()
{
    Name = "SampleAgent",
    Instructions = "Sample Instructions.",
    Kernel = kernel,
    Thread = new MyAgentChatThread("Custom Config Setting")
};
```

</td>
<td>

Construct thread manually with settings.

```csharp
var thread = new MyAgentChatThread("Custom Config Setting")
```

</td>
</tr>

<tr>
<td>Multi-agent Group Chat</td>
<td>

Supported by having an `AddChatMessageAsync` method that can be used to propogate messages from other agents.

```csharp
abstract class GroupConversationAgent : StatefulAgent
{
    public abstract async Task AddChatMessageAsync(
        ChatMessageContent chatMessageContent,
        CancellationToken cancellationToken? = default);
}
```

</td>
<td>

Supported by having an `AddChatMessageAsync` method that can be used to propogate messages from other agents.

```csharp
abstract class GroupConversationAgent : Agent
{
    public abstract async Task<AddMessageResponse> AddChatMessageAsync(
        ChatMessageContent chatMessageContent,
        ChatThread? thread = default,
        CancellationToken cancellationToken? = default);
}

class AddMessageResponse
{
    public ChatThread thread { get; }
}
```

</td>
</tr>

<tr>
<td>Multi-agent Processes with handoff</td>
<td>N/A</td>
<td>

Can be used by a higher level runner that for each step creates a new `ChatThread` with the outputs and handoffs of the previous agents and invokes the next `Agent`.
Probably makes sense to be able to add memory components to both the runner and to each `Agent`.
Distinguishing betweeen memory components that will save user information and those that won't is important here, so that there is no accidental user data leakage.

Since there is no threading on the lower level agents, perhaps they should take a modified memory component type that doesn't have the threading methods on it.

</td>
</tr>
</table>

## Starting a conversation with a new thread

### Higher Level Abstraction

Agent class instance is stateful and manages thread and memory inside.
Memory components / thread instances are passed once and lives as long as the agent instance lives.

```csharp
var agent = new MyAgent()
{
    Name = "CreditorsAgent",
    Instructions = "You are able to help pay creditors and ensure that invoices are processed and receipts are consolidated.",
    Kernel = kernel,
    Extensions = [new UserPreferencesMemoryComponent(kernel)],
    // Optional:
    Thread = new MyAgentChatThread()
};

var response = await agent.InvokeAsync("Has invoice 33-22-6324 from Fabrikam been paid yet?");
var response = await agent.InvokeAsync("And invoice 33-22-6325?");

Console.WriteLine(agent.ThreadId);
await agent.EndThread();
```

### Lower Level Abstraction

Agent class is stateless and thread and memory is managed separately.
Memory components / thread instances are passed per run, and their lifetimes are managed separately from the agent.
Since they are associated with the run, and multiple agents may participate in a run, it may not be
possible to limit memory components to specific agents via this mechanism.

With manual thread creation.

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

var response = await agent.InvokeAsync("Has invoice 33-22-6324 from Fabrikam been paid yet?", thread);
response = await agent.InvokeAsync("And invoice 33-22-6325?", response.thread);

Console.WriteLine(response.Thread.ThreadId);
await thread.EndThread();
```

With auto thread creation.

```csharp
var agent = new MyAgent()
{
    Name = "CreditorsAgent",
    Instructions = "You are able to help pay creditors and ensure that invoices are processed and receipts are consolidated.",
    Kernel = kernel
}

var response = await agent.InvokeAsync("Has invoice 33-22-6324 from Fabrikam been paid yet?");
response = await agent.InvokeAsync("And invoice 33-22-6325?", response.Thread);

Console.WriteLine(response.Thread.ThreadId);
await response.Thread.EndThread();
```

## Resuming a conversation on an existing thread

### Higher Level Abstraction

```csharp
var agent = new MyAgent()
    {
        Name = "CreditorsAgent",
        Instructions = "You are able to help pay creditors and ensure that invoices are processed and receipts are consolidated.",
        Kernel = kernel,
        Thread = new MyAgentChatThread("12345"),
        Extensions = [new UserPreferencesMemoryComponent(kernel)]
    };
var response = await agent.InvokeAsync("Has invoice 33-22-6324 from Fabrikam been paid yet?");
```

### Lower Level Abstraction

```csharp
var agent = new MyAgent()
    {
        Name = "CreditorsAgent",
        Instructions = "You are able to help pay creditors and ensure that invoices are processed and receipts are consolidated.",
        Kernel = kernel
    }
var thread = new MyAgentChatThread("12345");
var response = await agent.InvokeAsync("Has invoice 33-22-6324 from Fabrikam been paid yet?", thread);
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

## Thread forking with Responses

### Higher Level Abstraction

```csharp
var agent = new ResponsesAgent()
    {
        model = "gpt-4o-mini"
        Kernel = kernel,
        Extensions = [new UserPreferencesMemoryComponent(kernel)],
    };

var response1 = await agent.InvokeAsync("tell me a joke");
var response2 = await agent.InvokeAsync("tell me another");

// Two branches of response 2:
var agent3 new ResponsesAgent(agent);
var agent4 new ResponsesAgent(agent) { Extensions = [new MemZeroMemoryComponent(kernel)] };
var response3 = await agent3.InvokeAsync("tell me another");
var response4 = await agent4.InvokeAsync("tell me another");
```

### Lower Level Abstraction

```csharp
var agent = new ResponsesAgent()
    {
        model = "gpt-4o-mini"
        Kernel = kernel
    };

var userPrefs = new UserPreferencesMemoryComponent(kernel);
var response1 = await agent.InvokeAsync("tell me a joke", [userPrefs]);
var response2 = await agent.InvokeAsync("tell me another", response1.Thread, [userPrefs]);

// Two branches of response 2:
// Potential issue: You will get a different outcome when doing the following with an AssistantsAgent to a ResponsesAgent.
var response3 = await agent.InvokeAsync("tell me another", response2.Thread);
 // Do the new memory components replace whats on the thread, feels weird to not have it passed to the thread.
var response4 = await agent.InvokeAsync("tell me another", new ResponsesThread(response2.Thread.ThreadId, [new MemZeroMemoryComponent(kernel)]));
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
var agent = new AzureAIAgent(definition, azureAIAgentsClient) { Kernel = kernel };

AgentThread thread =  = await azureAIAgentsClient.CreateThreadAsync();

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

## Comparison of proposal with existing agents

### Lower level approach

The following is a list of proposed methods to add to the existing Agent base type:

1. Change different `InvokeAsync` methods to single design that takes `ChatMessageContent` and `ChatThread` as input.
1. Response should contain `ChatThread`, that can be modified from input due to forks.
1. Modify all `Agent` implementations to integrate with `MemoryManager` and invoke it on required lifecycle events, e.g. AI Invocation, messages added, etc.
1. TODO: NEEDS MORE INVESTIGATION: Add a common `CreateAgentAsync` method, that can create the agent in the service if that is required.

```csharp
abstract class Agent
{
    ...

    // TODO: NEEDS MORE INVESTIGATION
    public abstract async Task<CreateAgentResponse> CreateAgentAsync(CancellationToken cancellationToken? = default);

    // TODO: Should we support agent tools override at invocation time?
    public abstract async Task<InvokeResponse> InvokeAsync(
        ChatMessageContent? chatMessageContent = default,
        ChatThread? thread = default,
        KernelArguments? arguments = null,
        Kernel? kernel = null,
        string? additionalInstructions = null,
        CancellationToken cancellationToken? = default);

    ...
}

class InvokeResponse
{
    public ChatMessageContent chatMessageContent { get; }
    public ChatThread thread { get; }
}
```

Additional possible work to support message propogation from other agents in a multi-agent conversation:

1. Add an `AddChatMessageAsync` to `Agent` implementations that support this, to allow propogating responses from other conversation partitipants (e.g. Agents), where invocation is not desirable.

To support agents in a group conversation with other agents, it must be possible to propogate messages from one agent to other agents without
the other agents responding.
Not all services support this type of interaction model in all modes.
E.g. when using responses, you can simulate this when managing chat history locally and using the mode that doesn't save history in the service.
An extension of `Agent` which adds this capability for agents/modes that support it, is therefore valuable, since a group conversation
can therefore be restricted to this agent subtype.

```csharp
abstract class GroupConversationAgent : Agent
{
    public abstract async Task<AddMessageResponse> AddChatMessageAsync(
        ChatMessageContent chatMessageContent,
        ChatThread? thread = default,
        CancellationToken cancellationToken? = default);
}

class AddMessageResponse
{
    public ChatThread thread { get; }
}
```

### Higher level approach

```csharp
abstract class StatefulAgent
{
    ...

    // TODO: NEEDS MORE INVESTIGATION
    public abstract async Task<ChatMessageContent> CreateAgentAsync(CancellationToken cancellationToken? = default);

    public abstract async Task InvokeAsync(
        ChatMessageContent? chatMessageContent = default,
        KernelArguments? arguments = null,
        Kernel? kernel = null,
        string? additionalInstructions = null,
        CancellationToken cancellationToken? = default);

    // TODO: NEEDS MORE THINKING
    public StatefulAgent Fork();

    ...
}

// Can be either a wrapper, or a direct implementation.
// E.g. for a wrapper.
new ChatCompletionAgent().WithState(/* Optional custom Thread */new ChatHistoryThread(), [new MemZeroMemoryComponent(kernel)]);
```

```csharp
abstract class GroupConversationAgent : StatefulAgent
{
    public abstract async Task AddChatMessageAsync(
        ChatMessageContent chatMessageContent,
        ChatThread? thread = default,
        CancellationToken cancellationToken? = default);
}
```
