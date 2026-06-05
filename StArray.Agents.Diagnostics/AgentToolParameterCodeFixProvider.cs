using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StArray.Agents.Diagnostics.Resources;

#pragma warning disable RS1038 // CodeFixProvider 与 SourceGenerator 同程序集：仅在 IDE 中使用，不影响命令行编译

namespace StArray.Agents.Diagnostics;

/// <summary>
/// CodeFix 提供器：为 SAF0001 和 SAF0002 提供自动修复。
/// SAF0001: 为缺少 [ToolParameter] 的参数自动添加该特性。
/// SAF0002: 为误用 [ToolParameter] 的参数自动移除该特性。
///
/// CodeFix provider: offers automatic fixes for SAF0001 and SAF0002.
/// SAF0001: auto-adds [ToolParameter] to parameters missing it.
/// SAF0002: auto-removes [ToolParameter] from parameters that don't need it.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AgentToolParameterCodeFixProvider))]
[Shared]
public class AgentToolParameterCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [AgentToolParameterAnalyzer.MissingParamDiagId, AgentToolParameterAnalyzer.OrphanParamDiagId];

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticId = diagnostic.Id;

        // 定位触发诊断的参数节点 / Locate the parameter node that triggered the diagnostic
        var paramNode = root.FindNode(diagnostic.Location.SourceSpan)
            .FirstAncestorOrSelf<ParameterSyntax>();
        if (paramNode is null) return;

        if (diagnosticId == AgentToolParameterAnalyzer.MissingParamDiagId)
        {
            // SAF0001: 添加 [ToolParameter] / Add [ToolParameter]
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: AnalyzerResources.GetString("CodeFix_AddToolParameter"),
                    createChangedDocument: ct => AddToolParameterAsync(context.Document, paramNode, ct),
                    equivalenceKey: "AddToolParameter"),
                diagnostic);
        }
        else if (diagnosticId == AgentToolParameterAnalyzer.OrphanParamDiagId)
        {
            // SAF0002 方案 A: 移除 [ToolParameter] / SAF0002 Option A: Remove [ToolParameter]
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: AnalyzerResources.GetString("CodeFix_RemoveToolParameter"),
                    createChangedDocument: ct => RemoveToolParameterAsync(context.Document, paramNode, ct),
                    equivalenceKey: "RemoveToolParameter"),
                diagnostic);

            // SAF0002 方案 B: 给方法添加 [AgentTool] / SAF0002 Option B: Add [AgentTool] to method
            var methodNode = paramNode.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            if (methodNode is not null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: AnalyzerResources.GetString("CodeFix_AddAgentTool"),
                        createChangedDocument: ct => AddAgentToolToMethodAsync(context.Document, methodNode, ct),
                        equivalenceKey: "AddAgentToolToMethod"),
                    diagnostic);
            }
        }
    }

    /// <summary>
    /// SAF0001 修复：为参数自动生成 [ToolParameter("AgentTool键_参数名")] 并插入。
    /// SAF0001 fix: auto-generate and insert [ToolParameter("AgentToolKey_paramName")] before the parameter.
    /// </summary>
    private static async Task<Document> AddToolParameterAsync(
        Document document,
        ParameterSyntax paramNode,
        CancellationToken ct)
    {
        // 获取所在方法，提取 [AgentTool] 的键值 / Get containing method, extract [AgentTool] key
        var methodNode = paramNode.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        var agentToolKey = GetAgentToolKey(methodNode);
        var paramName = paramNode.Identifier.Text;

        // 生成 "{AgentTool键}_{参数名}" / Generate "{AgentToolKey}_{paramName}"
        var key = string.IsNullOrEmpty(agentToolKey)
            ? "Param_" + paramName
            : agentToolKey + "_" + paramName;

        // 构造 [ToolParameter("...")] 特性 / Build [ToolParameter("...")] attribute
        var attrArgs = SyntaxFactory.AttributeArgumentList(
            SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.AttributeArgument(
                    SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,
                        SyntaxFactory.Literal(key)))));
        var attr = SyntaxFactory.Attribute(
            SyntaxFactory.IdentifierName("ToolParameter"), attrArgs);
        var attrList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attr));

        // 插入到参数之前，注意保留原有缩进和换行
        // Insert before the parameter, preserving indentation and line breaks
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null) return document;

        // 在参数前追加新特性行（保持已有特性的换行风格）
        // Prepend new attribute line before the parameter
        var leadingTrivia = paramNode.GetLeadingTrivia();
        var newParam = paramNode.WithLeadingTrivia(SyntaxFactory.TriviaList())
                                .WithAttributeLists(
                                    paramNode.AttributeLists.Insert(0, attrList.WithLeadingTrivia(leadingTrivia)));
        // Restore original leading trivia to the first attribute
        var firstAttr = newParam.AttributeLists[0];
        newParam = newParam.WithAttributeLists(
            newParam.AttributeLists.Replace(firstAttr, firstAttr.WithLeadingTrivia(leadingTrivia))
        );
        // Ensure a single space between attribute and parameter type
        var lastIdx = newParam.AttributeLists.Count - 1;
        var lastAttr = newParam.AttributeLists[lastIdx];
        newParam = newParam.WithAttributeLists(
            newParam.AttributeLists.Replace(lastAttr,
                lastAttr.WithTrailingTrivia(SyntaxFactory.Space))
        );

        var newRoot = root.ReplaceNode(paramNode, newParam);
        return document.WithSyntaxRoot(newRoot);
    }

    /// <summary>
    /// SAF0002 修复：从参数上移除 [ToolParameter] 特性。
    /// SAF0002 fix: remove the [ToolParameter] attribute from the parameter.
    /// </summary>
    private static async Task<Document> RemoveToolParameterAsync(
        Document document,
        ParameterSyntax paramNode,
        CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null) return document;

        // 找到包含 [ToolParameter] 的特性列表并移除 / Find and remove the attribute list containing [ToolParameter]
        var newParam = paramNode;
        var attrListsToKeep = new List<AttributeListSyntax>();

        foreach (var attrList in paramNode.AttributeLists)
        {
            var attrsToKeep = new List<AttributeSyntax>();

            foreach (var attr in attrList.Attributes)
            {
                var attrName = attr.Name.ToString();
                if (attrName is "ToolParameter" or "ToolParameterAttribute")
                {
                    // 跳过 [ToolParameter]，不保留 / Skip [ToolParameter], don't keep
                    continue;
                }
                attrsToKeep.Add(attr);
            }

            if (attrsToKeep.Count > 0)
            {
                attrListsToKeep.Add(attrList.WithAttributes(
                    SyntaxFactory.SeparatedList(attrsToKeep)));
            }
            // 如果该 attrList 的 attributes 全部被移除，丢弃整行
            // If all attributes in this attrList are removed, drop the entire line
        }

        newParam = newParam.WithAttributeLists(SyntaxFactory.List(attrListsToKeep));

        // 如果原有特性的尾随换行还在，需要确保类型前仍有换行
        // Ensure there's still a newline before the type after removing attributes
        if (attrListsToKeep.Count > 0)
        {
            var lastIdx = attrListsToKeep.Count - 1;
            var last = attrListsToKeep[lastIdx];
            attrListsToKeep[lastIdx] = last.WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);
            newParam = newParam.WithAttributeLists(SyntaxFactory.List(attrListsToKeep));
        }

        var newRoot = root.ReplaceNode(paramNode, newParam);
        return document.WithSyntaxRoot(newRoot);
    }

    /// <summary>
    /// 从方法声明的 [AgentTool("xxx")] 特性中提取键值。
    /// Extracts the key value from the [AgentTool("xxx")] attribute on a method declaration.
    /// </summary>
    private static string GetAgentToolKey(MethodDeclarationSyntax? methodNode)
    {
        if (methodNode is null) return string.Empty;

        foreach (var attrList in methodNode.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var attrName = attr.Name.ToString();
                if (attrName is "AgentTool" or "AgentToolAttribute"
                    && attr.ArgumentList?.Arguments.Count > 0)
                {
                    var arg = attr.ArgumentList.Arguments[0];
                    var expr = arg.Expression;
                    if (expr is LiteralExpressionSyntax literal
                        && literal.Kind() == SyntaxKind.StringLiteralExpression)
                    {
                        return literal.Token.ValueText;
                    }
                }
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// SAF0002 方案 B：为方法添加 [AgentTool("Tool_ClassName_MethodName")]。
    /// SAF0002 Option B: add [AgentTool("Tool_ClassName_MethodName")] to the method.
    /// </summary>
    private static async Task<Document> AddAgentToolToMethodAsync(
        Document document,
        MethodDeclarationSyntax methodNode,
        CancellationToken ct)
    {
        // 构造键名：Tool_{类名}_{方法名} / Build key: Tool_{ClassName}_{MethodName}
        var classNode = methodNode.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        var className = classNode?.Identifier.Text ?? "Unknown";
        var methodName = methodNode.Identifier.Text;
        var key = $"Tool_{className}_{methodName}";

        // 构造 [AgentTool("...")] 特性 / Build [AgentTool("...")] attribute
        var attrArgs = SyntaxFactory.AttributeArgumentList(
            SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.AttributeArgument(
                    SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,
                        SyntaxFactory.Literal(key)))));
        var attr = SyntaxFactory.Attribute(
            SyntaxFactory.IdentifierName("AgentTool"), attrArgs);
        var attrList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attr));

        // 在方法已有特性列表开头插入 / Prepend to method's attribute lists
        // 把方法原有的 leading trivia（空行+缩进）移到新特性上，方法自身清空
        // Move the method's original leading trivia to the new attribute, clear it from the method
        var leadingTrivia = methodNode.GetLeadingTrivia();
        var newMethod = methodNode
            .WithLeadingTrivia(SyntaxFactory.TriviaList())
            .WithAttributeLists(
                methodNode.AttributeLists.Insert(0,
                    attrList.WithLeadingTrivia(leadingTrivia)));

        // 修正首特性的前导和末特性的尾随换行 / Fix leading/trailing trivia
        var firstAttr = newMethod.AttributeLists[0];
        newMethod = newMethod.WithAttributeLists(
            newMethod.AttributeLists.Replace(firstAttr, firstAttr.WithLeadingTrivia(leadingTrivia)));
        var lastIdx = newMethod.AttributeLists.Count - 1;
        var lastAttr = newMethod.AttributeLists[lastIdx];
        newMethod = newMethod.WithAttributeLists(
            newMethod.AttributeLists.Replace(lastAttr,
                lastAttr.WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed)));

        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null) return document;

        var newRoot = root.ReplaceNode(methodNode, newMethod);
        return document.WithSyntaxRoot(newRoot);
    }
}
