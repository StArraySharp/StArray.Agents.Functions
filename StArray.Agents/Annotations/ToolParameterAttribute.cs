using System.ComponentModel;
using System.Resources;
using StArray.Agents.Resources;

namespace StArray.Agents.Annotations;

/// <summary>
/// 标记方法参数，描述通过本地化键从资源文件加载。
/// Marks a method parameter; description loaded from resource file via localization key.
/// </summary>
public class ToolParameterAttribute(string key, bool isLocalize = true, Type? resourceType = null)
    : DescriptionAttribute
{
    private readonly Type _resourceType = resourceType ?? typeof(Localization);

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