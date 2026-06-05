using StArray.Agents.Abstraction;
using StArray.Agents.Annotations;
using StArray.Agents.Core;

namespace StArray.Agents.Tools;

/// <summary>
/// 数学工具集：提供加减乘除等基本运算。
/// Math tools: provides basic arithmetic operations (add, subtract, multiply, divide).
/// </summary>
public partial class MathTools : ITools
{
    [AgentTool("Tool_Math_Add")]
    public ToolResponse<int> Add(
        [ToolParameter("Tool_Math_Add_a")] int a,
        [ToolParameter("Tool_Math_Add_b")] int b)
    {
        return ToolResponse<int>.Success(a + b);
    }

    [AgentTool("Tool_Math_Subtract")]
    public ToolResponse<int> Subtract(
        [ToolParameter("Tool_Math_Subtract_a")] int a,
        [ToolParameter("Tool_Math_Subtract_b")] int b)
    {
        return ToolResponse<int>.Success(a - b);
    }

    [AgentTool("Tool_Math_Multiply")]
    public ToolResponse<int> Multiply(
        [ToolParameter("Tool_Math_Multiply_a")] int a,
        [ToolParameter("Tool_Math_Multiply_b")] int b)
    {
        return ToolResponse<int>.Success(a * b);
    }

    [AgentTool("Tool_Math_Divide")]
    public ToolResponse<double> Divide(
        [ToolParameter("Tool_Math_Divide_a")] double a,
        [ToolParameter("Tool_Math_Divide_b")] double b)
    {
        return b != 0
            ? ToolResponse<double>.Success(a / b)
            : ToolResponse<double>.Failure("除数不能为零。");
    }
}