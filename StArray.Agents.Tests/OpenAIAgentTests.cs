using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace StArray.Agents.Tests;

/// <summary>
/// OpenAI Agent 基础测试：创建、多轮对话、流式输出、ChatClientAgent 构造函数。
/// OpenAI Agent basics: creation, multi-turn conversation, streaming, ChatClientAgent constructor.
/// 需要 OPENAI_API_KEY 环境变量。 / Requires OPENAI_API_KEY environment variable.
/// </summary>
public class OpenAIAgentTests : TestBase
{
    [Test]
    public async Task CreateAgent_And_Converse()
    {
        AIAgent agent = CreateAgent(
            instructions: "You answer questions very concisely, in one sentence.",
            name: "ConciseBot");

        AgentResponse response = await agent.RunAsync("What is the capital of France?");
        string text = response.ToString();
        TestContext.Progress.WriteLine(text);
        Assert.That(text, Is.Not.Empty);
        Assert.That(text.ToLowerInvariant(), Does.Contain("paris"));
    }

    [Test]
    public async Task MultiTurnConversation_RemembersContext()
    {
        AIAgent agent = CreateAgent(
            instructions: "You are a helpful assistant. Keep answers short.",
            name: "SessionBot");

        AgentSession session = await agent.CreateSessionAsync();
        
        AgentRunOptions options = new AgentRunOptions
        {
            AdditionalProperties = new AdditionalPropertiesDictionary()
        };
        options.AdditionalProperties["thinking"] = "disabled";
        // 第一轮 / First turn
        await agent.RunAsync("My name is Alice.", session,options);

        // 第二轮 / Second turn
        AgentResponse response = await agent.RunAsync("What is my name?", session, options);
        string text = response.ToString();
        TestContext.Progress.WriteLine(text);
        Assert.That(text.ToLowerInvariant(), Does.Contain("alice"));
    }

    [Test]
    public async Task StreamingResponse_ReturnsChunks()
    {
        AIAgent agent = CreateAgent(
            instructions: "You are good at telling jokes.",
            name: "Joker");

        var fullText = "";
        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(
            "Tell me a short joke about programming."))
        {
            fullText += update.ToString();
        }

        TestContext.Progress.WriteLine(fullText);
        Assert.That(fullText, Is.Not.Empty);
    }

    [Test]
    public async Task ChatClientAgent_Constructor_DirectIChatClient()
    {
        IChatClient chatClient = CreateOpenAiClient()
            .GetChatClient(Model)
            .AsIChatClient();

        ChatClientAgent agent = new(
            chatClient: chatClient,
            instructions: "Reply in exactly one word.",
            name: "OneWordBot");

        AgentResponse response = await agent.RunAsync("What color is the sky on a clear day?");
        string text = response.ToString().Trim().ToLowerInvariant();
        TestContext.Progress.WriteLine(text);
        Assert.That(text, Is.Not.Empty);
    }
}
