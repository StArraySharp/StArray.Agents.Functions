using System.Diagnostics;
using System.Runtime.InteropServices;
using StArray.Agents.Abstraction;
using StArray.Agents.Annotations;
using StArray.Agents.Core;

namespace StArray.Agents.Tools;

/// <summary>
/// Shell 工具集：命令行执行、进程管理。
/// Shell tools: command execution, process management.
/// </summary>
public partial class ShellTools : ITools
{
    private const int DefaultTimeoutMs = 30_000;

    [AgentTool("Tool_Shell_Execute", NeedAgree = true)]
    public ToolResponse<string> Execute(
        [ToolParameter("Tool_Shell_Execute_command")] string command,
        [ToolParameter("Tool_Shell_Execute_shell")] string? shell = null,
        [ToolParameter("Tool_Shell_Execute_workingDir")] string? workingDir = null,
        [ToolParameter("Tool_Shell_Execute_timeout")] int timeoutMs = DefaultTimeoutMs)
    {
        try
        {
            var (fileName, args) = ResolveShell(command, shell ?? GetDefaultShell());
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDir ?? Environment.CurrentDirectory,
            };

            using var process = Process.Start(psi);
            if (process is null)
                return ToolResponse<string>.Failure("无法启动进程。");

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            if (!process.WaitForExit(timeoutMs))
            {
                process.Kill(entireProcessTree: true);
                return ToolResponse<string>.Failure($"命令执行超时（{timeoutMs}ms）。");
            }

            var result = output;
            if (!string.IsNullOrWhiteSpace(error))
                result += "\n[stderr]\n" + error;

            return ToolResponse<string>.Success(string.IsNullOrWhiteSpace(result) ? "(无输出)" : result.Trim());
        }
        catch (Exception ex)
        {
            return ToolResponse<string>.Failure($"执行命令失败：{ex.Message}");
        }
    }

    private static (string FileName, string Arguments) ResolveShell(string command, string shell)
    {
        return shell.ToLower() switch
        {
            "powershell" => ("powershell.exe", $"-NoProfile -Command \"{command}\""),
            "bash" => ("/bin/bash", $"-c \"{command}\""),
            "sh" => ("/bin/sh", $"-c \"{command}\""),
            "cmd" => ("cmd.exe", $"/c \"{command}\""),
            _ => IsWindows() ? ("cmd.exe", $"/c \"{command}\"") : ("/bin/bash", $"-c \"{command}\""),
        };
    }

    private static string GetDefaultShell() => IsWindows() ? "cmd" : "bash";

    [AgentTool("Tool_Shell_GetEnv")]
    public ToolResponse<string> GetEnv(
        [ToolParameter("Tool_Shell_GetEnv_name")] string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return value is not null
            ? ToolResponse<string>.Success(value)
            : ToolResponse<string>.Failure($"环境变量 '{name}' 未设置。");
    }

    [AgentTool("Tool_Shell_SetEnv")]
    public ToolResponse<string> SetEnv(
        [ToolParameter("Tool_Shell_SetEnv_name")] string name,
        [ToolParameter("Tool_Shell_SetEnv_value")] string value)
    {
        Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.Process);
        
        return ToolResponse<string>.Success($"环境变量 '{name}' 已设置。");
    }

    private static bool IsWindows() =>
        RuntimeInformation.IsOSPlatform(
            OSPlatform.Windows);
}