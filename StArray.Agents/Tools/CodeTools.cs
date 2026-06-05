using System.Text.Json;
using StArray.Agents.Abstraction;
using StArray.Agents.Annotations;
using StArray.Agents.Core;

namespace StArray.Agents.Tools;

/// <summary>
/// 代码工具集：提供 JSON 校验、代码行数统计等功能。
/// Code tools: provides JSON validation, line counting, and other code utilities.
/// </summary>
public partial class CodeTools : ITools
{
    [AgentTool("Tool_Code_ValidateJson")]
    public ToolResponse<string> ValidateJson(
        [ToolParameter("Tool_Code_ValidateJson_json")] string json)
    {
        try
        {
            JsonDocument.Parse(json);
            return ToolResponse<string>.Success("有效的 JSON。");
        }
        catch (Exception ex)
        {
            return ToolResponse<string>.Failure($"无效的 JSON：{ex.Message}");
        }
    }

    [AgentTool("Tool_Code_CountLines")]
    public static ToolResponse<int> CountLines(
        [ToolParameter("Tool_Code_CountLines_code")] string code)
    {
        if (string.IsNullOrEmpty(code))
            return ToolResponse<int>.Success(0);
        return ToolResponse<int>.Success(code.Split('\n', '\r').Length);
    }
}