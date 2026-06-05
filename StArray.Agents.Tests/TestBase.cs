using System.ClientModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace StArray.Agents.Tests;

/// <summary>
/// 测试基类：提供共享的 API 配置和 OpenAI 客户端创建。
/// Test base: provides shared API configuration and OpenAI client creation.
/// </summary>
public abstract class TestBase
{
    // ---- 环境变量配置 ----
    // Environment variable configuration
    // OPENAI_API_KEY       你的 OpenAI API Key
    // OPENAI_CHAT_MODEL    模型名称  Model name (default: gpt-4o-mini)
    // OPENAI_API_URL       API 端点  API endpoint (optional, defaults to OpenAI official)

    protected static string ApiKey =>
        Environment.GetEnvironmentVariable("OPENAI_API_KEY")
        ?? throw new InvalidOperationException("请设置环境变量 OPENAI_API_KEY / Set env OPENAI_API_KEY");

    protected static string Model =>
        Environment.GetEnvironmentVariable("OPENAI_CHAT_MODEL") ?? "gpt-4o-mini";

    protected static string? ApiUrl =>
        Environment.GetEnvironmentVariable("OPENAI_API_URL");

    /// <summary>
    /// 创建 OpenAI 客户端，支持自定义 API URL。
    /// Creates an OpenAI client with optional custom API URL.
    /// </summary>
    protected static OpenAIClient CreateOpenAiClient()
    {
        var options = new OpenAIClientOptions();
        if (!string.IsNullOrWhiteSpace(ApiUrl))
        {
            options.Endpoint = new Uri(ApiUrl);
        }
        return new OpenAIClient(new ApiKeyCredential(ApiKey), options);
    }

    /// <summary>
    /// 检测当前 API URL 是否为 DeepSeek。
    /// Detects whether the current API URL is DeepSeek.
    /// </summary>
    protected static bool IsDeepSeek => ApiUrl is not null && ApiUrl.Contains("deepseek", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 快捷创建带默认指令的 Agent，自动根据 API URL 选择创建方式。
    /// Shortcut to create an Agent, auto-detects DeepSeek vs OpenAI.
    /// </summary>
    protected static AIAgent CreateAgent(string instructions, string name, IList<AITool>? tools = null)
    {
        if (IsDeepSeek)
            return DeepSeekHelper.CreateDeepSeekAgent(ApiKey, Model, ApiUrl!, instructions, name, tools);

        return CreateOpenAiClient().GetChatClient(Model).AsAIAgent(instructions, name, tools: tools);
    }

    [SetUp]
    public virtual void Setup() { }
}
