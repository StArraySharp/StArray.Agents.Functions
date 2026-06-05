using StArray.Agents.Abstraction;
using StArray.Agents.Annotations;
using StArray.Agents.Core;

namespace StArray.Agents.Tools;

/// <summary>
/// 时间工具集：提供当前时间、UTC 时间和日期运算。
/// Time tools: provides current time, UTC time, and date arithmetic.
/// </summary>
public partial class TimeTools : ITools
{
    [AgentTool("Tool_Time_Now")]
    public ToolResponse<string> GetNow()
    {
        return ToolResponse<string>.Success(DateTime.Now.ToString("O"));
    }

    [AgentTool("Tool_Time_UtcNow")]
    public ToolResponse<string> GetUtcNow()
    {
        return ToolResponse<string>.Success(DateTime.UtcNow.ToString("O"));
    }

    [AgentTool("Tool_Time_AddDays")]
    public ToolResponse<string> AddDays(
        [ToolParameter("Tool_Time_AddDays_dateString")] string dateString,
        [ToolParameter("Tool_Time_AddDays_days")] int days)
    {
        if (DateTime.TryParse(dateString, out var date))
        {
            return ToolResponse<string>.Success(date.AddDays(days).ToString("O"));
        }
        return ToolResponse<string>.Failure("无效的日期格式。");
    }
}