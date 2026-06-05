namespace StArray.Agents.Core;
/// <summary>
/// 工具结果：包含工具调用的结果数据和相关信息。
/// Tool result: contains the result data and related information of a tool invocation.
/// </summary>
public readonly record struct ToolResponse<TResult>(TResult? Result, bool IsSuccess, string? ErrorMessage = null)
{
    /// <summary>
    /// 工具调用的结果数据。
    /// The result data of the tool invocation.
    /// </summary>
    public TResult? Result { get; } = Result;

    /// <summary>
    /// 工具调用是否成功。
    /// Whether the tool invocation was successful.
    /// </summary>
    public bool IsSuccess { get; } = IsSuccess;

    /// <summary>
    /// 错误信息（如果调用失败）。
    /// Error message (if the invocation failed).
    /// </summary>
    public string? ErrorMessage { get; } = ErrorMessage;

    /// <summary>
    /// 创建一个成功的工具结果。
    /// Creates a successful tool result.
    /// </summary>
    /// <param name="result">工具调用的结果数据。The result data of the tool invocation.</param>
    /// <returns>成功的工具结果。A successful tool result.</returns>
    public static ToolResponse<TResult> Success(TResult result) => new(result, true);

    /// <summary>
    /// 创建一个失败的工具结果。
    /// Creates a failed tool result.
    /// </summary>
    /// <param name="errorMessage">错误信息。The error message.</param>
    /// <returns>失败的工具结果。A failed tool result.</returns>
    public static ToolResponse<TResult> Failure(string errorMessage) => new(default, false, errorMessage);
}
