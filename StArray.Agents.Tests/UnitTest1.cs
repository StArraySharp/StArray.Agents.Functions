using System.ClientModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using StArray.Agents.Core;
using StArray.Agents.Tools;

namespace StArray.Agents.Tests;

public class Tests
{
    // ---- 环境变量配置 ----
    // Environment variable configuration
    // OPENAI_API_KEY       你的 OpenAI API Key
    // OPENAI_CHAT_MODEL    模型名称  Model name (default: gpt-4o-mini)
    // OPENAI_API_URL       API 端点  API endpoint (optional, defaults to OpenAI official)

    private static string ApiKey =>
        Environment.GetEnvironmentVariable("OPENAI_API_KEY")
        ?? throw new InvalidOperationException("请设置环境变量 OPENAI_API_KEY");

    private static string Model =>
        Environment.GetEnvironmentVariable("OPENAI_CHAT_MODEL") ?? "gpt-4o-mini";

    private static string? ApiUrl =>
        Environment.GetEnvironmentVariable("OPENAI_API_URL");

    private static OpenAIClient CreateOpenAiClient()
    {
        var options = new OpenAIClientOptions();
        if (!string.IsNullOrWhiteSpace(ApiUrl))
        {
            options.Endpoint = new Uri(ApiUrl);
        }
        return new OpenAIClient(new ApiKeyCredential(ApiKey), options);
    }

    [SetUp]
    public void Setup()
    {
        
    }

    // ========================================================================
    // 测试 1：本地函数工具 — 验证 AgentToolAttribute 与资源本地化
    // Test 1: Local function tool — verify AgentToolAttribute with resource localization
    // ========================================================================
    [Test]
    public void Test_LocalFunctionDescription()
    {
        var timeTools = new TimeTools();
        var aiFunction = AIFunctionFactory.Create(timeTools.GetNow);
        TestContext.Progress.WriteLine(aiFunction.Description);
        Assert.That(aiFunction.Description, Is.EqualTo("获取当前本地时间"));
    }

    // ========================================================================
    // 测试 2：MAF 基础 — 使用 OpenAI ChatClient 创建 Agent 并对话
    // Test 2: MAF basics — create Agent with OpenAI ChatClient and converse
    // ========================================================================
    [Test]
    public async Task Test_CreateAgent_OpenAI_ChatClient()
    {
        // 方式一：new OpenAIClient(apiKey).GetChatClient(model).AsAIAgent(...)
        // Approach 1: new OpenAIClient(apiKey).GetChatClient(model).AsAIAgent(...)
        AIAgent agent = CreateOpenAiClient()
            .GetChatClient(Model)
            .AsAIAgent(
                instructions: "You answer questions very concisely, in one sentence.",
                name: "ConciseBot");

        AgentResponse response = await agent.RunAsync("What is the capital of France?");
        string text = response.ToString();
        TestContext.Progress.WriteLine(text);
        Assert.That(text, Is.Not.Empty);
        Assert.That(text.ToLowerInvariant(), Does.Contain("paris"));
    }

    // ========================================================================
    // 测试 3：MAF + 函数工具 — 通过 FunctionFactory 属性注册工具
    // Test 3: MAF + function tools — register tools via FunctionFactory properties
    // ========================================================================
    [Test]
    public async Task Test_AgentWithFunctionTools()
    {
        // 使用源生成器自动生成的属性：FunctionFactory.TimeTools, FunctionFactory.MathTools
        // Use source-generator-generated properties: FunctionFactory.TimeTools, FunctionFactory.MathTools
        var tools = new List<AITool>();
        tools.AddRange(FunctionFactory.TimeTools.GetTools());
        tools.AddRange(FunctionFactory.MathTools.GetTools());

        AIAgent agent = CreateOpenAiClient()
            .GetChatClient(Model)
            .AsAIAgent(
                instructions: """
                    You are a helpful assistant that can access the current UTC time
                    and perform math calculations. Use the tools when appropriate.
                    """,
                name: "ToolBot",
                tools: tools);

        // 问一个需要工具的问题
        // Ask a question that requires tools
        AgentResponse response = await agent.RunAsync(
            "What is 123 + 456? Also tell me the current UTC time.");
        string text = response.ToString();
        TestContext.Progress.WriteLine(text);
        Assert.That(text, Is.Not.Empty);
        Assert.That(text, Does.Contain("579"));
    }

    // ========================================================================
    // 测试 4：MAF 多轮对话 — 使用 AgentSession 在多个回合间保持上下文
    // Test 4: MAF multi-turn conversation — use AgentSession to maintain context across turns
    // ========================================================================
    [Test]
    public async Task Test_MultiTurnConversation()
    {
        AIAgent agent = CreateOpenAiClient()
            .GetChatClient(Model)
            .AsAIAgent(
                instructions: "You are a helpful assistant. Keep answers short.",
                name: "SessionBot");

        // 创建会话以在多轮之间保持状态
        // Create a session to persist state across multiple turns
        AgentSession session = await agent.CreateSessionAsync();

        // 第一轮
        // First turn
        await agent.RunAsync("My name is Alice.", session);

        // 第二轮：Agent 应该记得名字
        // Second turn: Agent should remember the name
        AgentResponse response = await agent.RunAsync("What is my name?", session);
        string text = response.ToString();
        TestContext.Progress.WriteLine(text);
        Assert.That(text.ToLowerInvariant(), Does.Contain("alice"));
    }

    // ========================================================================
    // 测试 5：MAF 流式输出 — RunStreamingAsync 逐块返回响应
    // Test 5: MAF streaming — RunStreamingAsync returns response chunk by chunk
    // ========================================================================
    [Test]
    public async Task Test_StreamingResponse()
    {
        AIAgent agent = CreateOpenAiClient()
            .GetChatClient(Model)
            .AsAIAgent(
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

    // ========================================================================
    // 测试 6：Agent 作为函数工具 — 将一个 Agent 暴露给另一个 Agent 调用
    // Test 6: Agent as function tool — expose one Agent as a callable tool to another Agent
    // ========================================================================
    [Test]
    public async Task Test_AgentAsFunctionTool()
    {
        // 子 Agent：专门计算
        // Child agent: specializes in calculation
        AIAgent mathAgent = CreateOpenAiClient()
            .GetChatClient(Model)
            .AsAIAgent(
                instructions: "You are a math assistant. Only compute and return the numeric result.",
                name: "MathAgent");

        // 父 Agent：可用子 Agent 作为工具
        // Parent agent: uses child agent as a tool
        AIAgent mainAgent = CreateOpenAiClient()
            .GetChatClient(Model)
            .AsAIAgent(
                instructions: "You are a helpful assistant. Use your tools when needed and respond in Chinese.",
                name: "MainAgent",
                tools: [mathAgent.AsAIFunction()]);

        AgentResponse response = await mainAgent.RunAsync("帮我算 256 乘以 7 等于多少？");
        string text = response.ToString();
        TestContext.Progress.WriteLine(text);
        Assert.That(text, Is.Not.Empty);
    }

    // ========================================================================
    // 测试 7：使用 FunctionFactory 批量注册本项目所有工具
    // Test 7: Use FunctionFactory to batch-register all tools in this project
    // ========================================================================
    [Test]
    public async Task Test_FunctionFactoryIntegration()
    {
        // 通过源生成器生成的静态属性获取所有工具集
        // Get all toolsets via source-generator-generated static properties
        var allTools = FunctionFactory.GetAllFunctions()
            .Cast<AITool>()
            .ToList();

        TestContext.Progress.WriteLine($"已加载 {allTools.Count} 个工具");
        Assert.That(allTools.Count, Is.GreaterThanOrEqualTo(1));

        AIAgent agent = CreateOpenAiClient()
            .GetChatClient(Model)
            .AsAIAgent(
                instructions: "You are a helpful assistant with various tools. Use them when needed.",
                name: "FullToolBot",
                tools: allTools);

        AgentResponse response = await agent.RunAsync("What time is it now (UTC)?");
        string text = response.ToString();
        TestContext.Progress.WriteLine(text);
        Assert.That(text, Is.Not.Empty);
    }

    // ========================================================================
    // 测试 8：ChatClientAgent 构造函数 — 直接使用 IChatClient 创建 Agent
    // Test 8: ChatClientAgent constructor — create Agent directly from IChatClient
    // ========================================================================
    [Test]
    public async Task Test_ChatClientAgent_Constructor()
    {
        // 方式二：先构造 IChatClient，再包装为 ChatClientAgent
        // Approach 2: construct IChatClient first, then wrap as ChatClientAgent
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

    // ========================================================================
    // 测试 9：使用 FunctionFactory 属性获取工具并验证参数描述
    // Test 9: Use FunctionFactory properties to get tools and verify parameter descriptions
    // ========================================================================
    [Test]
    public async Task Test_ToolWithParameterDescriptions()
    {
        // 通过源生成器生成的静态属性获取工具集
        // Get toolsets via source-generator-generated static properties
        var tools = new List<AITool>();
        tools.AddRange(FunctionFactory.TextTools.GetTools());

        AIAgent agent = CreateOpenAiClient()
            .GetChatClient(Model)
            .AsAIAgent(
                instructions: "You have text tools. Use them to answer questions about text processing.",
                name: "TextToolBot",
                tools: tools);

        AgentResponse response = await agent.RunAsync(
            "Convert 'hello world' to uppercase and tell me how many characters it has.");
        string text = response.ToString();
        TestContext.Progress.WriteLine(text);
        Assert.That(text, Does.Contain("11").Or.Contain("HELLO WORLD"));
    }
}
