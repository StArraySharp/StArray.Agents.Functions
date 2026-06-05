using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using StArray.Agents.Diagnostics.Resources;

#pragma warning disable RS2008 // 分析器发布跟踪（本项目中不需要）
#pragma warning disable RS1033 // 中文句号已满足要求，Roslyn 仅检查 ASCII 标点
#pragma warning disable RS1038 // Analyzer 与 CodeFixProvider 同程序集
#pragma warning disable RS1012 // 无操作时 RegisterSyntaxNodeAction 在 lambda 外，RS1012 误报

namespace StArray.Agents.Diagnostics;

/// <summary>
/// 分析器：双向检测 [AgentTool] 与 [ToolParameter] 的匹配性。
/// 诊断消息通过 AnalyzerResources.resx 本地化。
/// <list type="bullet">
/// <item>SAF0001: [AgentTool] 方法参数缺少 [ToolParameter]</item>
/// <item>SAF0002: 非 [AgentTool] 方法的参数误用了 [ToolParameter]</item>
/// <item>SAF0003: 本地化键在 _resourceType 指定的资源文件中缺失（isLocalize=true）</item>
/// </list>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AgentToolParameterAnalyzer : DiagnosticAnalyzer
{
    public const string MissingParamDiagId = "SAF0001";
    public const string OrphanParamDiagId = "SAF0002";
    public const string MissingLocKeyDiagId = "SAF0003";

    private const string Category = "Usage";

    private static readonly DiagnosticDescriptor MissingParamRule = new(
        MissingParamDiagId,
        new LocalizableResourceString("SAF0001_Title", AnalyzerResources.ResourceManager, typeof(AnalyzerResources)),
        new LocalizableResourceString("SAF0001_MessageFormat", AnalyzerResources.ResourceManager, typeof(AnalyzerResources)),
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true,
        new LocalizableResourceString("SAF0001_Description", AnalyzerResources.ResourceManager, typeof(AnalyzerResources)));

    private static readonly DiagnosticDescriptor OrphanParamRule = new(
        OrphanParamDiagId,
        new LocalizableResourceString("SAF0002_Title", AnalyzerResources.ResourceManager, typeof(AnalyzerResources)),
        new LocalizableResourceString("SAF0002_MessageFormat", AnalyzerResources.ResourceManager, typeof(AnalyzerResources)),
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true,
        new LocalizableResourceString("SAF0002_Description", AnalyzerResources.ResourceManager, typeof(AnalyzerResources)));

    private static readonly DiagnosticDescriptor MissingLocKeyRule = new(
        MissingLocKeyDiagId,
        new LocalizableResourceString("SAF0003_Title", AnalyzerResources.ResourceManager, typeof(AnalyzerResources)),
        new LocalizableResourceString("SAF0003_MessageFormat", AnalyzerResources.ResourceManager, typeof(AnalyzerResources)),
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true,
        new LocalizableResourceString("SAF0003_Description", AnalyzerResources.ResourceManager, typeof(AnalyzerResources)));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [MissingParamRule, OrphanParamRule, MissingLocKeyRule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
    {
        var methodDecl = (MethodDeclarationSyntax)context.Node;
        var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDecl, context.CancellationToken);
        if (methodSymbol is not IMethodSymbol { IsAbstract: false }) return;

        var agentToolAttr = FindOurAttribute(methodSymbol.GetAttributes(), "AgentToolAttribute", "AgentTool");
        var hasAgentTool = agentToolAttr != null;

        // SAF0003: [AgentTool] 本地化键检查
        if (agentToolAttr != null)
        {
            CheckLocKey(context, agentToolAttr, methodDecl.Identifier.Text,
                methodDecl.AttributeLists, "AgentTool", "AgentToolAttribute", methodDecl.GetLocation());
        }

        if (methodDecl.ParameterList.Parameters.Count == 0) return;

        foreach (var param in methodSymbol.Parameters)
        {
            var toolParamAttr = FindOurAttribute(param.GetAttributes(), "ToolParameterAttribute", "ToolParameter");

            if (hasAgentTool && toolParamAttr == null)
            {
                // SAF0001
                context.ReportDiagnostic(Diagnostic.Create(MissingParamRule,
                    param.Locations.FirstOrDefault(), param.Name, methodDecl.Identifier.Text));
            }
            else if (!hasAgentTool && toolParamAttr != null)
            {
                // SAF0002
                context.ReportDiagnostic(Diagnostic.Create(OrphanParamRule,
                    param.Locations.FirstOrDefault(), param.Name, methodDecl.Identifier.Text));
            }
            else if (toolParamAttr != null)
            {
                // SAF0003: [ToolParameter] 本地化键检查
                var paramSyntax = methodDecl.ParameterList.Parameters[param.Ordinal];
                var fallbackLocation = param.Locations.FirstOrDefault();
                CheckLocKey(context, toolParamAttr, param.Name,
                    paramSyntax.AttributeLists, "ToolParameter", "ToolParameterAttribute", fallbackLocation);
            }
        }
    }

    /// <summary>
    /// 检查特性的本地化键是否存在于其 _resourceType 指向的资源文件中。
    /// </summary>
    private static void CheckLocKey(
        SyntaxNodeAnalysisContext context,
        AttributeData attr,
        string elementName,
        SyntaxList<AttributeListSyntax> attributeLists,
        string attrShortName,
        string attrFullName,
        Location? fallbackLocation)
    {
        if (!ExtractBoolArg(attr, "isLocalize", true)) return;

        var locKey = ExtractFirstStringArg(attr);
        if (string.IsNullOrWhiteSpace(locKey)) return;

        // 从特性的 resourceType 参数获取目标资源类型
        var resourceType = ExtractTypeArg(attr, "resourceType")
            ?? FindLocalizationType(context.Compilation);
        if (resourceType == null) return;

        var keys = GetLocKeys(resourceType);
        if (keys.Count == 0 || keys.Contains(locKey!)) return;

        var location = GetAttributeFirstArgLocation(attributeLists, attrShortName, attrFullName)
            ?? fallbackLocation;
        context.ReportDiagnostic(Diagnostic.Create(MissingLocKeyRule, location,
            elementName, locKey, attrShortName, resourceType.Name));
    }

    /// <summary>从资源类型符号中提取静态字符串属性名集合。</summary>
    private static HashSet<string> GetLocKeys(INamedTypeSymbol resourceType)
    {
        var keys = new HashSet<string>();
        foreach (var member in resourceType.GetMembers())
        {
            if (member is IPropertySymbol { IsStatic: true } prop
                && prop.Type.SpecialType == SpecialType.System_String
                && prop.Name is not "ResourceManager" and not "Culture")
            {
                keys.Add(prop.Name);
            }
        }
        return keys;
    }

    /// <summary>从特性的命名参数或构造函数参数中提取 Type 参数值。</summary>
    private static INamedTypeSymbol? ExtractTypeArg(AttributeData attr, string paramName)
    {
        foreach (var kvp in attr.NamedArguments)
        {
            if (kvp.Key == paramName && kvp.Value.Value is INamedTypeSymbol type)
                return type;
        }

        if (attr.AttributeConstructor != null)
        {
            var parameters = attr.AttributeConstructor.Parameters;
            for (int i = 0; i < parameters.Length && i < attr.ConstructorArguments.Length; i++)
            {
                if (parameters[i].Name == paramName
                    && attr.ConstructorArguments[i].Value is INamedTypeSymbol ctorType)
                    return ctorType;
            }
        }

        return null;
    }

    /// <summary>在编译中查找 Localization 类型（resourceType 为 null 时的回退）。</summary>
    private static INamedTypeSymbol? FindLocalizationType(Compilation compilation)
    {
        return compilation.GetTypeByMetadataName("StArray.Agents.Resources.Localization");
    }

    /// <summary>在特性列表中查找来自本库的目标特性，返回 AttributeData 或 null。</summary>
    private static AttributeData? FindOurAttribute(
        ImmutableArray<AttributeData> attributes, string name1, string name2)
    {
        return attributes.FirstOrDefault(attr =>
            attr.AttributeClass is { } ac
            && (ac.Name == name1 || ac.Name == name2)
            && ac.ContainingNamespace?.ToDisplayString() == "StArray.Agents.Annotations");
    }

    private static string? ExtractFirstStringArg(AttributeData attr)
    {
        if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string key)
            return key;
        return null;
    }

    private static bool ExtractBoolArg(AttributeData attr, string paramName, bool defaultValue)
    {
        // 先查命名参数（如 isLocalize: false）
        foreach (var kvp in attr.NamedArguments)
        {
            if (kvp.Key == paramName && kvp.Value.Value is bool boolVal)
                return boolVal;
        }

        // 再查构造函数位置参数（如 [AgentTool("key", false)]）
        if (attr.AttributeConstructor != null)
        {
            var parameters = attr.AttributeConstructor.Parameters;
            for (int i = 0; i < parameters.Length && i < attr.ConstructorArguments.Length; i++)
            {
                if (parameters[i].Name == paramName
                    && attr.ConstructorArguments[i].Value is bool ctorBool)
                    return ctorBool;
            }
        }

        return defaultValue;
    }

    private static Location? GetAttributeFirstArgLocation(
        SyntaxList<AttributeListSyntax> attributeLists, string name1, string name2)
    {
        foreach (var attrList in attributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var attrName = attr.Name.ToString();
                if ((attrName == name1 || attrName == name2) && attr.ArgumentList?.Arguments.Count > 0)
                    return attr.ArgumentList.Arguments[0].GetLocation();
            }
        }
        return null;
    }
}
