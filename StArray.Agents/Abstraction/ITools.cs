using System.Runtime.InteropServices;
using Microsoft.Extensions.AI;

namespace StArray.Agents.Abstraction;

/// <summary>
/// 工具集接口，所有 Agent 工具类必须实现此接口。
/// Toolset interface; all Agent tool classes must implement this interface.
/// </summary>
public interface ITools
{
    /// <summary>
    /// 返回当前工具集包含的所有 AIFunction。
    /// Returns all AIFunction instances contained in this toolset.
    /// </summary>
    List<AIFunction> GetTools();
}