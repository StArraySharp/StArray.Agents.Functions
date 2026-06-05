using System.ClientModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using StArray.Agents.Core;
using ChatMessage = OpenAI.Chat.ChatMessage;
using MEAI = Microsoft.Extensions.AI;

#pragma warning disable SCME0001

namespace StArray.Agents.Cli;

/// <summary>
/// 纯控制台分割视图 TUI，无第三方依赖。
/// Zero-dependency console split-view TUI.
/// </summary>
public class TuiApp
{
    private static AIAgent? _agent;
    private static CancellationTokenSource? _currentCts;
    private static readonly List<MEAI.ChatMessage> _history = [];
    private static readonly List<string> _msgLines = []; // displayed message lines
    private static readonly List<string> _inputHistory = [];
    private static int _historyIdx;
    private static int _topLine;
    private static bool _isGenerating;
    private static string _inputText = "";
    private static int _cursorPos;
    private static int _inputAreaTop;

    private const int InputHeight = 3; // 输入区 + 状态栏

    public static void Run()
    {
        _agent = CreateAgent();
        Console.OutputEncoding = Encoding.UTF8;
        Console.CursorVisible = true;

        Console.Clear();
        _inputAreaTop = Console.WindowHeight - InputHeight;
        DrawSeparator();

        _msgLines.Add($"StArray Chat | {Env("OPENAI_CHAT_MODEL") ?? "unknown"}");
        _msgLines.Add(new string('-', 50));
        RenderMessages();

        // Ctrl+C handler
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Console.CursorVisible = true;
            Console.SetCursorPosition(0, Console.WindowHeight - 1);
            Console.WriteLine("Exiting...");
            SaveSession();
            Environment.Exit(0);
        };

        while (true)
        {
            DrawInputPrompt();
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                var text = _inputText.Trim();
                _inputText = ""; _cursorPos = 0;

                if (text == "/exit") break;
                if (string.IsNullOrWhiteSpace(text)) continue;

                _inputHistory.Add(text);
                _historyIdx = _inputHistory.Count;
                _history.Add(new MEAI.ChatMessage(ChatRole.User, text));
                _msgLines.Add("");
                _msgLines.Add($"You> {text}");
                RenderMessages();

                StreamResponse().GetAwaiter().GetResult();
                _isGenerating = false;
            }
            else if (_isGenerating && key.Key == ConsoleKey.Escape)
            {
                _currentCts?.Cancel();
            }
            else if (key.Key == ConsoleKey.UpArrow && _historyIdx > 0)
            {
                _historyIdx--;
                _inputText = _inputHistory[_historyIdx];
                _cursorPos = _inputText.Length;
            }
            else if (key.Key == ConsoleKey.DownArrow)
            {
                if (_historyIdx < _inputHistory.Count - 1)
                { _historyIdx++; _inputText = _inputHistory[_historyIdx]; }
                else
                { _historyIdx = _inputHistory.Count; _inputText = ""; }
                _cursorPos = _inputText.Length;
            }
            else if (key.Key == ConsoleKey.Backspace && _cursorPos > 0)
            {
                _inputText = _inputText[..(_cursorPos - 1)] + _inputText[_cursorPos..];
                _cursorPos--;
            }
            else if (key.Key == ConsoleKey.LeftArrow && _cursorPos > 0)
            {
                _cursorPos--;
            }
            else if (key.Key == ConsoleKey.RightArrow && _cursorPos < _inputText.Length)
            {
                _cursorPos++;
            }
            else if (key.KeyChar >= ' ')
            {
                _inputText = _inputText[.._cursorPos] + key.KeyChar + _inputText[_cursorPos..];
                _cursorPos++;
            }
        }

        Console.Clear();
        SaveSession();
    }

    private static void DrawInputPrompt()
    {
        // 清输入区
        Console.SetCursorPosition(0, _inputAreaTop);
        Console.Write(new string(' ', Console.WindowWidth));
        Console.SetCursorPosition(0, _inputAreaTop + 1);
        Console.Write(new string(' ', Console.WindowWidth));
        // 重新绘制
        Console.SetCursorPosition(0, _inputAreaTop);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("─".PadRight(Console.WindowWidth, '─'));
        Console.ResetColor();
        Console.SetCursorPosition(0, _inputAreaTop + 1);
        Console.Write("> " + _inputText + " ");
        Console.SetCursorPosition(2 + _cursorPos, _inputAreaTop + 1);
    }

    private static async Task StreamResponse()
    {
        if (_agent is null) return;

        while (true)
        {
            _isGenerating = true;
            _currentCts = new CancellationTokenSource();

            var toolNames = new Dictionary<string, string>();
            var fullText = new StringBuilder();
            var lastWasTool = false;
            var stopped = false;
            var pendingApprovals = new List<ToolApprovalRequestContent>();

            try
            {
                await foreach (var update in _agent.RunStreamingAsync(_history, cancellationToken: _currentCts.Token))
                {
                    if (_currentCts.IsCancellationRequested) break;
                    if (update.Contents is not { Count: > 0 }) continue;

                    foreach (var content in update.Contents)
                    {
                        switch (content)
                        {
                            case ToolApprovalRequestContent approval:
                                pendingApprovals.Add(approval);
                                break;
                            case TextContent text:
                                AppendToLastLine(text.Text);
                                fullText.Append(text.Text);
                                lastWasTool = false;
                                break;
                            case FunctionCallContent call:
                                toolNames[call.CallId] = call.Name;
                                if (!lastWasTool) _msgLines.Add("");
                                _msgLines.Add($"  [{call.Name}]");
                                RenderMessages();
                                lastWasTool = true;
                                break;
                            case FunctionResultContent result:
                                var rName = toolNames.GetValueOrDefault(result.CallId, "?");
                                var rOutput = ExtractResultText(result.Result?.ToString());
                                _msgLines.Add(string.IsNullOrEmpty(rOutput) ? $"  [{rName}]" : $"  [{rName}: {Truncate(rOutput, 60)}]");
                                RenderMessages();
                                lastWasTool = true;
                                break;
                        }
                    }
                }
            }
            catch (OperationCanceledException) { stopped = true; }
            finally { _currentCts.Dispose(); _currentCts = null; }

            if (fullText.Length > 0)
                _history.Add(new MEAI.ChatMessage(ChatRole.Assistant, fullText.ToString()));

            // 处理待审批的工具调用
            if (pendingApprovals.Count > 0)
            {
                _isGenerating = false;
                var approved = PromptApprovals(pendingApprovals);
                AddApprovalResponses(pendingApprovals, approved);

                if (approved) continue; // 继续 agent 循环执行工具
                _msgLines.Add("  [已拒绝]");
                RenderMessages();
            }

            if (stopped) { _msgLines[^1] += " [stopped]"; RenderMessages(); }
            break;
        }
    }

    /// <summary>提示用户确认工具调用。</summary>
    private static bool PromptApprovals(List<ToolApprovalRequestContent> approvals)
    {
        foreach (var req in approvals)
        {
            var fcc = req.ToolCall as FunctionCallContent;
            var name = fcc?.Name ?? req.ToolCall?.GetType().Name ?? "?";
            var args = fcc?.Arguments is { Count: > 0 } dict
                ? string.Join(", ", dict.Select(kv => $"{kv.Key}={kv.Value}"))
                : "";

            _msgLines.Add("");
            _msgLines.Add($"  ⚠ 允许调用 [{name}]{(args.Length > 0 ? $"({args})" : "")}? [y/N]");
            RenderMessages();
        }

        // 读取用户输入
        _inputText = ""; _cursorPos = 0;
        while (true)
        {
            DrawInputPrompt();
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                var answer = _inputText.Trim().ToLower();
                _inputText = ""; _cursorPos = 0;
                return answer == "y" || answer == "yes";
            }
            if (key.Key == ConsoleKey.Escape) { _inputText = ""; _cursorPos = 0; return false; }
            if (key.Key == ConsoleKey.Backspace && _cursorPos > 0)
            {
                _inputText = _inputText[..(_cursorPos - 1)] + _inputText[_cursorPos..];
                _cursorPos--;
            }
            else if (key.KeyChar >= ' ')
            {
                _inputText = _inputText[.._cursorPos] + key.KeyChar + _inputText[_cursorPos..];
                _cursorPos++;
            }
        }
    }

    /// <summary>将审批结果添加到聊天历史。</summary>
    private static void AddApprovalResponses(List<ToolApprovalRequestContent> approvals, bool approved)
    {
        var responses = new List<AIContent>();
        foreach (var req in approvals)
        {
            responses.Add(new ToolApprovalResponseContent(req.RequestId, approved, req.ToolCall));
        }
        _history.Add(new MEAI.ChatMessage(ChatRole.User, responses));
    }

    private static int _aiStartLine = -1;

    private static void AppendToLastLine(string text)
    {
        if (_aiStartLine < 0) { _msgLines.Add(""); _aiStartLine = _msgLines.Count - 1; }

        _msgLines[_msgLines.Count - 1] += text;

        // 更新屏幕上最后一行
        int msgAreaHeight = _inputAreaTop - 1;
        int lineIdx = _msgLines.Count - 1 - _topLine;
        if (lineIdx >= 0 && lineIdx < msgAreaHeight)
        {
            Console.SetCursorPosition(0, lineIdx);
            Console.Write(_msgLines[_msgLines.Count - 1].PadRight(Console.WindowWidth));
        }
    }

    private static void RenderMessages()
    {
        _aiStartLine = -1;
        int msgAreaHeight = _inputAreaTop - 1;
        int visible = msgAreaHeight;

        // Ensure topLine doesn't cut off bottom
        if (_msgLines.Count > visible) _topLine = _msgLines.Count - visible;
        else _topLine = 0;

        for (int row = 0; row < visible; row++)
        {
            int srcIdx = _topLine + row;
            Console.SetCursorPosition(0, row);
            if (srcIdx < _msgLines.Count)
                Console.Write(_msgLines[srcIdx].PadRight(Console.WindowWidth));
            else
                Console.Write(new string(' ', Console.WindowWidth));
        }
    }

    private static void DrawSeparator()
    {
        Console.SetCursorPosition(0, _inputAreaTop);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("─".PadRight(Console.WindowWidth, '─'));
        Console.ResetColor();
    }

    private static AIAgent CreateAgent()
    {
        var apiKey = Env("OPENAI_API_KEY") ?? "";
        var model = Env("OPENAI_CHAT_MODEL") ?? "gpt-4o-mini";
        var apiUrl = Env("OPENAI_API_URL");
        var isDeepSeek = apiUrl?.Contains("deepseek", StringComparison.OrdinalIgnoreCase) == true;

        var options = new OpenAIClientOptions();
        if (!string.IsNullOrWhiteSpace(apiUrl)) options.Endpoint = new Uri(apiUrl);
        var client = new OpenAIClient(new ApiKeyCredential(apiKey), options);
        var tools = FunctionFactory.GetAllFunctions().Cast<AITool>().ToList();

        var builder = client.GetChatClient(model).AsIChatClient().AsBuilder();
        builder.UseFunctionInvocation();
        if (isDeepSeek) builder.Use(Patches.PatchReasoning, Patches.PatchStreamingReasoning);

        return builder.BuildAIAgent(new ChatClientAgentOptions
        {
            Name = "StArray",
            ChatOptions = new ChatOptions { Instructions = "You are a helpful assistant. Use tools when needed. Respond in Chinese.", Tools = tools },
        });
    }

    private static void SaveSession()
    {
        if (_history.Count == 0) return;
        var dir = Path.Combine(Environment.CurrentDirectory, "sessions");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"chat_{DateTime.Now:yyyyMMdd_HHmmss}.md");
        using var w = new StreamWriter(file, false, Encoding.UTF8);
        w.WriteLine($"# StArray Chat\n**Date:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n**Model:** {Env("OPENAI_CHAT_MODEL") ?? "unknown"}\n\n---");
        foreach (var m in _history)
            w.WriteLine(m.Role == ChatRole.User ? $"\n**You>** {m.Text}" : $"\n**AI>** {m.Text}");
    }

    private static string? Env(string n) => Environment.GetEnvironmentVariable(n);
    private static string Truncate(string s, int m) => s.Length <= m ? s : s[..(m - 3)] + "...";

    private static string? ExtractResultText(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { using var d = JsonDocument.Parse(json!); return d.RootElement.TryGetProperty("result", out var r) ? r.ToString() : json; }
        catch { return json; }
    }
}

public static class Patches
{
    public static async Task<ChatResponse> PatchReasoning(IEnumerable<MEAI.ChatMessage> msgs, ChatOptions? opts, IChatClient inner, CancellationToken ct)
    {
        var resp = await inner.GetResponseAsync(msgs, opts, ct);
        if (resp.RawRepresentation is not ChatCompletion cc) return resp;
        var json = cc.Patch.GetJson("$.choices[0].message"u8);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("reasoning_content", out var el)) return resp;
        var text = el.GetString();
        if (string.IsNullOrWhiteSpace(text)) return resp;
        foreach (var msg in resp.Messages)
        {
            if (msg.Role != ChatRole.Assistant) continue;
            var p = ChatMessage.CreateAssistantMessage(cc);
            p.Patch.Set("$.reasoning_content"u8, text);
            msg.RawRepresentation = p;
        }
        return resp;
    }

    public static async IAsyncEnumerable<ChatResponseUpdate> PatchStreamingReasoning(IEnumerable<MEAI.ChatMessage> msgs, ChatOptions? opts, IChatClient inner, [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var u in inner.GetStreamingResponseAsync(msgs, opts, ct))
        {
            if (u.RawRepresentation is StreamingChatCompletionUpdate s)
            {
                var j = s.Patch.GetJson("$.choices[0].delta"u8);
                using var d = JsonDocument.Parse(j);
                if (d.RootElement.TryGetProperty("reasoning_content", out var e) && !string.IsNullOrWhiteSpace(e.GetString()))
                { u.AdditionalProperties ??= []; u.AdditionalProperties["ReasoningContent"] = e.GetString(); }
            }
            yield return u;
        }
    }
}
