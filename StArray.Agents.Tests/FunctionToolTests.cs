using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using StArray.Agents.Core;

namespace StArray.Agents.Tests;

/// <summary>
/// 函数工具集成测试：工具注册、Agent 嵌套、工厂批量注册、参数描述。
/// Function tool integration tests: tool registration, Agent nesting, factory batch registration, parameter descriptions.
/// 需要 OPENAI_API_KEY 环境变量。 / Requires OPENAI_API_KEY environment variable.
/// </summary>
public class FunctionToolTests : TestBase
{
    [Test]
    public async Task RegisterTools_ViaFunctionFactoryProperties()
    {
        var tools = new List<AITool>();
        tools.AddRange(FunctionFactory.TimeTools.GetTools());
        tools.AddRange(FunctionFactory.MathTools.GetTools());

        AIAgent agent = CreateAgent(
            instructions: """
                You are a helpful assistant that can access the current UTC time
                and perform math calculations. Use the tools when appropriate.
                """,
            name: "ToolBot",
            tools: tools);

        AgentResponse response = await agent.RunAsync(
            "What is 123 + 456? Also tell me the current UTC time.");
        string text = response.ToString();
        TestContext.Progress.WriteLine(text);
        Assert.That(text, Is.Not.Empty);
        Assert.That(text, Does.Contain("579"));
    }

    [Test]
    public async Task AgentAsFunctionTool_NestedAgents()
    {
        AIAgent mathAgent = CreateAgent(
            instructions: "You are a math assistant. Only compute and return the numeric result.",
            name: "MathAgent");

        AIAgent mainAgent = CreateAgent(
            instructions: "You are a helpful assistant. Use your tools when needed and respond in Chinese.",
            name: "MainAgent",
            tools: [mathAgent.AsAIFunction()]);

        AgentResponse response = await mainAgent.RunAsync("帮我算 256 乘以 7 等于多少？");
        string text = response.ToString();
        TestContext.Progress.WriteLine(text);
        Assert.That(text, Is.Not.Empty);
    }

    [Test]
    public async Task FunctionFactory_GetAllFunctions_BatchRegistration()
    {
        var allTools = FunctionFactory.GetAllFunctions()
            .Cast<AITool>()
            .ToList();

        TestContext.Progress.WriteLine($"已加载 {allTools.Count} 个工具 / Loaded {allTools.Count} tools");
        Assert.That(allTools.Count, Is.GreaterThanOrEqualTo(1));

        AIAgent agent = CreateAgent(
            instructions: "You are a helpful assistant with various tools. Use them when needed.",
            name: "FullToolBot",
            tools: allTools);

        AgentResponse response = await agent.RunAsync("What time is it now (UTC)?");
        string text = response.ToString();
        TestContext.Progress.WriteLine(text);
        Assert.That(text, Is.Not.Empty);
    }

    [Test]
    public async Task TextTools_WithParameterDescriptions()
    {
        var tools = new List<AITool>();
        tools.AddRange(FunctionFactory.TextTools.GetTools());

        AIAgent agent = CreateAgent(
            instructions: "You have text tools. Use them to answer questions about text processing.",
            name: "TextToolBot",
            tools: tools);

        AgentResponse response = await agent.RunAsync(
            "Convert 'hello world' to uppercase and tell me how many characters it has.");
        string text = response.ToString();
        TestContext.Progress.WriteLine(text);
        Assert.That(text, Is.Not.Empty);
    }

    [Test]
    public async Task FileTools_RegisterAndUse()
    {
        var tools = new List<AITool>();
        tools.AddRange(FunctionFactory.FileTools.GetTools());

        AIAgent agent = CreateAgent(
            instructions: "You have file tools. Use them when asked about files.",
            name: "FileBot",
            tools: tools);

        AgentResponse response = await agent.RunAsync(
            "Can you check if a file exists at 'C:\\Windows\\explorer.exe'?");
        string text = response.ToString();
        TestContext.Progress.WriteLine(text);
        Assert.That(text, Is.Not.Empty);
    }

    [Test]
    public async Task CodeTools_RegisterAndUse()
    {
        var tools = new List<AITool>();
        tools.AddRange(FunctionFactory.CodeTools.GetTools());

        AIAgent agent = CreateAgent(
            instructions: "You have code tools. Use them when asked about code.",
            name: "CodeBot",
            tools: tools);

        AgentResponse response = await agent.RunAsync(
            """Is this valid JSON: {"name": "test", "value": 42}?""");
        string text = response.ToString();
        TestContext.Progress.WriteLine(text);
        Assert.That(text, Is.Not.Empty);
    }
}
