using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

#pragma warning disable RS1038 // SourceGenerator 与 CodeFixProvider 同程序集

namespace StArray.Agents.Diagnostics;

/// <summary>
/// 源生成器：扫描所有实现了 <see cref="ITools"/> 的 partial 类（含子目录），
/// 自动生成 GetTools() 方法体（基于 [AgentTool] 标记的方法），
/// 同时为 <see cref="FunctionFactory"/> 生成静态属性、GetBuiltInFunctions 和 GetAllFunctions 实现。
/// Source generator: scans all partial classes implementing <see cref="ITools"/> (including subdirectories),
/// auto-generates GetTools() bodies (based on [AgentTool]-marked methods),
/// and generates static properties, GetBuiltInFunctions, and GetAllFunctions for <see cref="FunctionFactory"/>.
/// </summary>
[Generator]
public class FunctionFactoryGenerator : IIncrementalGenerator
{
    private const string AgentToolAttributeName = "AgentToolAttribute";
    private const string AgentToolAttributeShortName = "AgentTool";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 收集所有 ITools 实现及其带 [AgentTool] 的方法
        // Collect all ITools implementations and their [AgentTool]-marked methods
        var toolsInfoProvider = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => node is ClassDeclarationSyntax cds
                                           && cds.BaseList is not null
                                           && cds.BaseList.Types.Count > 0,
            transform: static (ctx, ct) =>
            {
                var classDecl = (ClassDeclarationSyntax)ctx.Node;
                var classSymbol = ctx.SemanticModel.GetDeclaredSymbol(classDecl, ct);

                if (classSymbol is null
                    || classSymbol.IsAbstract
                    || classSymbol.IsStatic
                    || !classSymbol.AllInterfaces.Any(i => i.Name == "ITools"))
                {
                    return null;
                }

                // 检查是否是 partial 类 / Check if it's a partial class
                var isPartial = classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
                if (!isPartial)
                {
                    // 不报错，静默跳过；用户需手动加 partial
                    // Don't error, silently skip; user needs to add partial manually
                    return null;
                }

                // 收集带 [AgentTool] 特性的 public 实例方法
                // Collect public instance methods with [AgentTool] attribute
                var agentMethods = new List<MethodInfo>();
                foreach (var member in classSymbol.GetMembers())
                {
                    if (member is IMethodSymbol method
                        && method.DeclaredAccessibility == Accessibility.Public
                        && !method.IsStatic
                        && !method.IsAbstract
                        && method.MethodKind == MethodKind.Ordinary)
                    {
                        foreach (var attr in method.GetAttributes())
                        {
                            if (attr.AttributeClass?.Name is AgentToolAttributeName or AgentToolAttributeShortName
                                && attr.AttributeClass.ContainingNamespace?.ToDisplayString() == "StArray.Agents.Annotations")
                            {
                                var needAgree = attr.NamedArguments.Any(
                                    kv => kv.Key == "NeedAgree" && kv.Value.Value is true);
                                agentMethods.Add(new MethodInfo(method.Name, needAgree));
                                break;
                            }
                        }
                    }
                }

                return new ToolsClassInfo(
                    classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    classSymbol.Name,
                    classSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty,
                    agentMethods);
            })
            .Where(static t => t is not null);

        var allInfo = toolsInfoProvider.Collect();

        // 为每个 ITools 类生成 GetTools() partial 文件
        // Generate GetTools() partial file for each ITools class
        context.RegisterSourceOutput(
            toolsInfoProvider,
            static (spc, info) =>
            {
                if (info is null) return;
                GenerateGetToolsPartial(spc, info);
            });

        // 生成 FunctionFactory.g.cs
        // Generate FunctionFactory.g.cs
        context.RegisterSourceOutput(
            allInfo,
            static (spc, infos) =>
            {
                GenerateFunctionFactory(spc, infos!);
            });
    }

    /// <summary>
    /// 为单个 ITools 类生成 GetTools() 的 partial 实现。
    /// Generates the GetTools() partial implementation for a single ITools class.
    /// </summary>
    private static void GenerateGetToolsPartial(
        SourceProductionContext context,
        ToolsClassInfo info)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        // 用命名空间包裹 / Wrap in namespace
        if (!string.IsNullOrEmpty(info.Namespace))
        {
            sb.AppendLine($"namespace {info.Namespace};");
            sb.AppendLine();
        }

        sb.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(" +
                      "\"StArray.Agents.Diagnostics.FunctionFactoryGenerator\", \"1.0.0.0\")]");
        sb.AppendLine("[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]");
        sb.AppendLine($"public partial class {info.ClassName}");
        sb.AppendLine("{");
        sb.AppendLine("    /// <inheritdoc/>");
        sb.AppendLine("    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(" +
                      "\"StArray.Agents.Diagnostics.FunctionFactoryGenerator\", \"1.0.0.0\")]");
        sb.AppendLine("    public global::System.Collections.Generic.List<global::Microsoft.Extensions.AI.AIFunction> GetTools()");
        sb.AppendLine("    {");
        sb.AppendLine("        return");
        sb.AppendLine("        [");
  
        for (var i = 0; i < info.AgentMethods.Count; i++)
        {
            var m = info.AgentMethods[i];
            var comma = i < info.AgentMethods.Count - 1 ? "," : "";
            var createCall = $"global::Microsoft.Extensions.AI.AIFunctionFactory.Create({m.Name})";
            var line = m.NeedAgree
                ? $"            new global::Microsoft.Extensions.AI.ApprovalRequiredAIFunction({createCall}){comma}"
                : $"            {createCall}{comma}";
            sb.AppendLine(line);
        }

        sb.AppendLine("        ];");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        var hintName = $"{info.ClassName}.GetTools.g.cs";
        context.AddSource(hintName, sb.ToString());
    }

    /// <summary>
    /// 生成 FunctionFactory 的 partial 类（静态属性 + GetBuiltInFunctions + GetAllFunctions）。
    /// Generates the FunctionFactory partial class (static properties + GetBuiltInFunctions + GetAllFunctions).
    /// </summary>
    private static void GenerateFunctionFactory(
        SourceProductionContext context,
        IReadOnlyList<ToolsClassInfo> infos)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace StArray.Agents.Core;");
        sb.AppendLine();
        sb.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(" +
                      "\"StArray.Agents.Diagnostics.FunctionFactoryGenerator\", \"1.0.0.0\")]");
        sb.AppendLine("[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]");
        sb.AppendLine("public static partial class FunctionFactory");
        sb.AppendLine("{");

        // 为每个 ITools 实现生成静态属性
        // Generate a static property for each ITools implementation
        foreach (var info in infos)
        {
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// 获取 {info.ClassName} 工具集。 / Gets the {info.ClassName} toolset.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(" +
                          "\"StArray.Agents.Diagnostics.FunctionFactoryGenerator\", \"1.0.0.0\")]");
            sb.AppendLine($"    public static {info.FullTypeName} {info.ClassName} {{ get; }} = new();");
            sb.AppendLine();
        }

        // 生成 GetBuiltInFunctions 方法（内置工具部分）
        // Generate GetBuiltInFunctions method (built-in tools part)
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// 返回所有内置工具的 AIFunction 列表。");
        sb.AppendLine("    /// Returns AIFunction list of all built-in tools.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(" +
                      "\"StArray.Agents.Diagnostics.FunctionFactoryGenerator\", \"1.0.0.0\")]");
        sb.AppendLine("    private static partial global::System.Collections.Generic.List<global::Microsoft.Extensions.AI.AIFunction> GetBuiltInFunctions()");
        sb.AppendLine("    {");
        sb.AppendLine("        var list = new global::System.Collections.Generic.List<global::Microsoft.Extensions.AI.AIFunction>();");
        foreach (var info in infos)
        {
            sb.AppendLine($"        list.AddRange({info.ClassName}.GetTools());");
        }
        sb.AppendLine("        return list;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // 生成 GetAllFunctions 方法（内置工具 + 自定义工具）
        // Generate GetAllFunctions method (built-in tools + custom tools)
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// 获取所有工具函数：内置工具 + 通过 AddTools 注册的自定义工具。");
        sb.AppendLine("    /// Gets all tool functions: built-in tools + custom tools registered via AddTools.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(" +
                      "\"StArray.Agents.Diagnostics.FunctionFactoryGenerator\", \"1.0.0.0\")]");
        sb.AppendLine("    public static partial global::System.Collections.Generic.IEnumerable<global::Microsoft.Extensions.AI.AIFunction> GetAllFunctions()");
        sb.AppendLine("    {");
        sb.AppendLine("        foreach (var f in GetBuiltInFunctions())");
        sb.AppendLine("            yield return f;");
        sb.AppendLine();
        sb.AppendLine("        // Can't yield inside lock, collect custom tools first");
        sb.AppendLine("        global::System.Collections.Generic.List<global::Microsoft.Extensions.AI.AIFunction>? custom = null;");
        sb.AppendLine("        lock (_lock)");
        sb.AppendLine("        {");
        sb.AppendLine("            custom = new global::System.Collections.Generic.List<global::Microsoft.Extensions.AI.AIFunction>();");
        sb.AppendLine("            foreach (var tools in _registeredTools.Values)");
        sb.AppendLine("                custom.AddRange(tools.GetTools());");
        sb.AppendLine("        }");
        sb.AppendLine("        foreach (var f in custom)");
        sb.AppendLine("            yield return f;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource("FunctionFactory.g.cs", sb.ToString());
    }

    /// <summary>
    /// 存储一个 ITools 实现类的元信息。
    /// Stores metadata about a single ITools implementation class.
    /// </summary>
    private sealed class ToolsClassInfo
    {
        public string FullTypeName { get; }
        public string ClassName { get; }
        public string Namespace { get; }
        public List<MethodInfo> AgentMethods { get; }

        public ToolsClassInfo(
            string fullTypeName,
            string className,
            string @namespace,
            List<MethodInfo> agentMethods)
        {
            FullTypeName = fullTypeName;
            ClassName = className;
            Namespace = @namespace;
            AgentMethods = agentMethods;
        }
    }

    private sealed class MethodInfo
    {
        public string Name { get; }
        public bool NeedAgree { get; }

        public MethodInfo(string name, bool needAgree)
        {
            Name = name;
            NeedAgree = needAgree;
        }
    }
}
