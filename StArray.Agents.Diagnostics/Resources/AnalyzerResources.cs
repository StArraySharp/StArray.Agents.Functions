using System.Reflection;
using System.Resources;

namespace StArray.Agents.Diagnostics.Resources;

/// <summary>
/// 分析器本地化资源访问器。通过 ResourceManager 从嵌入的 .resx 读取诊断消息。
/// Analyzer localization resource accessor. Reads diagnostic messages from embedded .resx via ResourceManager.
/// </summary>
internal static class AnalyzerResources
{
    /// <summary>ResourceManager 实例，供 LocalizableResourceString 使用。</summary>
    internal static readonly ResourceManager ResourceManager = new(
        $"{typeof(AnalyzerResources).Namespace}.AnalyzerResources",
        typeof(AnalyzerResources).GetTypeInfo().Assembly);

    /// <summary>获取指定键的本地化字符串。 / Gets the localized string for the given key.</summary>
    internal static string GetString(string key)
    {
        return ResourceManager.GetString(key) ?? $"<missing resource: {key}>";
    }
}
