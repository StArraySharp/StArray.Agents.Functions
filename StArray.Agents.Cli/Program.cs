using System.ClientModel;
using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using StArray.Agents.Core;

namespace StArray.Agents.Cli;

class Program
{
    /// <summary>true = Terminal.Gui TUI, false = 纯控制台交互</summary>
    private const bool UseTui = false;

    private static AIAgent? _agent;
    private static CancellationTokenSource? _currentCts;
    private static readonly List<ChatMessage> _history = [];
    private static readonly List<string> _inputHistory = [];
    private static int _historyIdx;

    static async Task Main(string[] args)
    {
        if (UseTui)
        {
            TuiApp.Run();
            return;
        }

        _agent = CreateAgent();
        Console.OutputEncoding = Encoding.UTF8;

        // Ctrl+C 优雅退出并保存会话
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\nExiting...");
            SaveSession();
            Environment.Exit(0);
        };

        Console.WriteLine($"StArray Chat | {Environment.GetEnvironmentVariable("OPENAI_CHAT_MODEL") ?? "unknown"}");
        Console.WriteLine("Up/Down: history | Esc: stop | /exit or Ctrl+C: quit & save");
        Console.WriteLine(new string('-', 50));

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("\nYou> ");
            Console.ResetColor();

            var input = ReadLineWithHistory();
            if (string.IsNullOrWhiteSpace(input)) continue;
            if (input == "/exit") break;

            _inputHistory.Add(input);
            _historyIdx = _inputHistory.Count;
            _history.Add(new ChatMessage(ChatRole.User, input));

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("AI>  ");
            Console.ResetColor();

            await StreamResponse();
            Console.WriteLine();
        }

        Console.WriteLine();
        SaveSession();
    }

    private static string ReadLineWithHistory()
    {
        var buffer = new List<char>();
        int cursor = 0;
        _historyIdx = _inputHistory.Count;

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return new string(buffer.ToArray()); }
            if (key.Key == ConsoleKey.UpArrow && _historyIdx > 0)
            {
                _historyIdx--;
                ClearBuffer(buffer); buffer.AddRange(_inputHistory[_historyIdx]); Console.Write(_inputHistory[_historyIdx]);
                cursor = buffer.Count;
                continue;
            }
            if (key.Key == ConsoleKey.DownArrow)
            {
                _historyIdx = _historyIdx < _inputHistory.Count - 1 ? _historyIdx + 1 : _inputHistory.Count;
                var t = _historyIdx < _inputHistory.Count ? _inputHistory[_historyIdx] : "";
                ClearBuffer(buffer); buffer.AddRange(t); Console.Write(t);
                cursor = buffer.Count;
                continue;
            }
            if (key.Key == ConsoleKey.Backspace && cursor > 0)
            {
                cursor--; buffer.RemoveAt(cursor); Console.Write("\b \b");
                for (int i = cursor; i < buffer.Count; i++) Console.Write(buffer[i]);
                for (int i = buffer.Count; i > cursor; i--) Console.Write("\b");
                continue;
            }
            if (key.KeyChar >= ' ')
            {
                buffer.Insert(cursor, key.KeyChar); cursor++;
                Console.Write(key.KeyChar);
                for (int i = cursor; i < buffer.Count; i++) Console.Write(buffer[i]);
                for (int i = buffer.Count; i > cursor; i--) Console.Write("\b");
            }
        }
    }

    private static void ClearBuffer(List<char> b) { while (b.Count > 0) { b.RemoveAt(b.Count - 1); Console.Write("\b \b"); } }

    private static async Task StreamResponse()
    {
        if (_agent is null) return;

        while (true)
        {
            _currentCts = new CancellationTokenSource();
            var stopped = false;

            _ = Task.Run(() =>
            {
                while (!_currentCts.IsCancellationRequested)
                {
                    if (Console.KeyAvailable && Console.ReadKey(intercept: true).Key == ConsoleKey.Escape)
                        _currentCts.Cancel();
                    Thread.Sleep(50);
                }
            });

            var toolNames = new Dictionary<string, string>();
            var fullText = "";
            var lastWasTool = false;
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
                                Console.Write(text.Text); fullText += text.Text; lastWasTool = false; break;
                            case FunctionCallContent call:
                                toolNames[call.CallId] = call.Name;
                                if (!lastWasTool) Console.WriteLine();
                                Console.ForegroundColor = ConsoleColor.DarkGray;
                                Console.WriteLine($"  [{call.Name}]");
                                Console.ResetColor();
                                lastWasTool = true; break;
                            case FunctionResultContent result:
                                var rName = toolNames.GetValueOrDefault(result.CallId, "?");
                                var rOutput = ExtractResultText(result.Result?.ToString());
                                Console.ForegroundColor = ConsoleColor.DarkGray;
                                Console.WriteLine(string.IsNullOrEmpty(rOutput) ? $"  [{rName}]" : $"  [{rName}: {Truncate(rOutput, 60)}]");
                                Console.ResetColor();
                                lastWasTool = true; break;
                        }
                    }
                }
            }
            catch (OperationCanceledException) { stopped = true; }
            finally { _currentCts.Dispose(); _currentCts = null; }

            if (stopped) { Console.ForegroundColor = ConsoleColor.Yellow; Console.Write(" [stopped]"); Console.ResetColor(); }
            if (!string.IsNullOrWhiteSpace(fullText))
                _history.Add(new ChatMessage(ChatRole.Assistant, fullText));

            // 处理待审批的工具调用
            if (pendingApprovals.Count > 0)
            {
                var approved = PromptApprovalsConsole(pendingApprovals);
                var responses = new List<AIContent>();
                foreach (var req in pendingApprovals)
                    responses.Add(new ToolApprovalResponseContent(req.RequestId, approved, req.ToolCall));
                _history.Add(new ChatMessage(ChatRole.User, responses));

                if (!approved) { Console.WriteLine("  [已拒绝]"); break; }
                continue;
            }

            break;
        }
    }

    private static bool PromptApprovalsConsole(List<ToolApprovalRequestContent> approvals)
    {
        foreach (var req in approvals)
        {
            var fcc = req.ToolCall as FunctionCallContent;
            var name = fcc?.Name ?? req.ToolCall?.GetType().Name ?? "?";
            var args = fcc?.Arguments is { Count: > 0 } dict
                ? string.Join(", ", dict.Select(kv => $"{kv.Key}={kv.Value}"))
                : "";

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"\n  ⚠ 允许调用 [{name}]{(args.Length > 0 ? $"({args})" : "")}? [y/N] ");
            Console.ResetColor();
        }
        var answer = Console.ReadLine()?.Trim().ToLower();
        return answer == "y" || answer == "yes";
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 3)] + "...";

    private static string? ExtractResultText(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json!);
            if (doc.RootElement.TryGetProperty("result", out var r)) return r.ToString();
            return json;
        }
        catch { return json; }
    }

    private static AIAgent CreateAgent()
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
        var model = Environment.GetEnvironmentVariable("OPENAI_CHAT_MODEL") ?? "gpt-4o-mini";
        var apiUrl = Environment.GetEnvironmentVariable("OPENAI_API_URL");
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
        w.WriteLine($"# StArray Chat Session\n**Date:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n**Model:** {Environment.GetEnvironmentVariable("OPENAI_CHAT_MODEL") ?? "unknown"}\n\n---");
        foreach (var m in _history)
            w.WriteLine(m.Role == ChatRole.User ? $"\n**You>** {m.Text}" : $"\n**AI>** {m.Text}");
        Console.WriteLine($"Saved: {file}");
    }
}
