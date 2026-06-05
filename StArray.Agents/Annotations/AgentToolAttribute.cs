using System.ComponentModel;
using System.Resources;
using StArray.Agents.Resources;

namespace StArray.Agents.Annotations;

/// <summary>
/// 标记方法为 Agent 可调用工具，描述通过本地化键从资源文件加载。
/// Marks a method as an Agent-callable tool; description loaded from resource file via localization key.
/// </summary>
public class AgentToolAttribute(string key, bool isLocalize = true, Type? resourceType = null)
    : DescriptionAttribute
{
    private readonly Type _resourceType = resourceType ?? typeof(Localization);
    public bool NeedAgree { get; set; }

    public override string Description
    {
        get
        {
            if (!isLocalize) return field;
            var rm = new ResourceManager(_resourceType);
            return rm.GetString(field) ?? field;
        }
    } = key;
}