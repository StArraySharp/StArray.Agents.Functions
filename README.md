# StArray.Agents

.NET Agent 工具函数库，基于 Microsoft Agent Framework (MAF)。

## 项目结构

```
StArray.Agents/
  StArray.Agents/                     -- 主项目
    Annotations/                     -- AgentToolAttribute, ToolParameterAttribute
    Core/                            -- FunctionFactory, ToolResponse<T>
    Tools/                           -- 工具实现 (MathTools, FileTools, TimeTools, TextTools, CodeTools)
      Win32/                         -- WindowTools
    Resources/                       -- 本地化 (.resx)
  StArray.Agents.Diagnostics/        -- 诊断项目
    FunctionFactoryGenerator         -- 源生成器：扫描 partial ITools 类，自动生成 GetTools()
    AgentToolParameterAnalyzer       -- 分析器：SAF0001 / SAF0002 / SAF0003 诊断
    AgentToolParameterCodeFixProvider -- CodeFix：自动添加/移除特性
    Resources/                       -- 分析器本地化
  StArray.Agents.Tests/              -- 测试项目
```

## 快速开始

```csharp
// 1. 定义一个 partial 工具类，实现 ITools
public partial class MyTools : ITools
{
    [AgentTool("Tool_My_DoSomething")]
    public ToolResponse<string> DoSomething(
        [ToolParameter("Tool_My_DoSomething_input")] string input)
    {
        return ToolResponse<string>.Success(input.ToUpper());
    }
}

// 2. 源生成器自动生成 GetTools()

// 3. 注册到 Agent
var tools = FunctionFactory.MyTools.GetTools();
var agent = new ChatClientAgent(...) { Tools = tools };
```

## FunctionFactory API

`FunctionFactory` 提供两种工具管理模式：

### 内置工具（编译时）

源生成器扫描所有 `partial class : ITools` 并自动生成静态属性，可直接使用：

```csharp
// 单个工具集
var mathTools = FunctionFactory.MathTools.GetTools();
var fileTools = FunctionFactory.FileTools.GetTools();

// 全部内置工具
var allBuiltIn = FunctionFactory.GetAllFunctions();
```

### 自定义工具（运行时）

用户可以在运行时动态注册自定义工具集：

```csharp
public class MyCustomTools : ITools
{
    public List<AIFunction> GetTools() { /* ... */ }
}

// 注册
FunctionFactory.AddTools(new MyCustomTools());
// 或指定键名
FunctionFactory.AddTools("my", new MyCustomTools());

// 获取全部工具（内置 + 自定义）
var all = FunctionFactory.GetAllFunctions();

// 按类型获取
var myTools = FunctionFactory.GetCustomTools<MyCustomTools>();

// 获取所有自定义工具快照
var allCustom = FunctionFactory.GetCustomTools();

// 移除 / 清空
FunctionFactory.RemoveTools<MyCustomTools>();
FunctionFactory.ClearCustomTools();
```

| 方法 | 说明 |
|------|------|
| `AddTools<T>(T tools)` | 按类型注册自定义工具集 |
| `AddTools<T>(string key, T tools)` | 按指定键名注册 |
| `RemoveTools<T>()` | 按类型移除 |
| `ClearCustomTools()` | 清空所有自定义工具 |
| `GetCustomTools()` | 获取所有自定义工具快照 |
| `GetCustomTools<T>()` | 按类型获取，强类型返回 |
| `GetAllFunctions()` | 内置 + 自定义全部工具 |

## 工具方法规范

- 类必须为 `partial class` 并实现 `ITools`
- 方法标记 `[AgentTool("Tool_XXX_YYY")]` ，键名格式 `Tool_{类名}_{方法名}`
- 每个参数标记 `[ToolParameter("Tool_XXX_YYY_paramName")]` ，键名格式 `{AgentTool键}_{参数名}`
- 返回类型使用 `ToolResponse<T>` 统一包装成功/失败

## 分析器诊断

| ID | 描述 | CodeFix |
|----|------|---------|
| SAF0001 | [AgentTool] 方法参数缺少 [ToolParameter] | ✅ |
| SAF0002 | 非 [AgentTool] 方法的参数误用 [ToolParameter] | ✅ |
| SAF0003 | 本地化键在资源文件中不存在（isLocalize=true） | ❌ |

SAF0001 和 SAF0002 提供 CodeFix（Alt+Enter）一键修复。

## 本地化

工具描述和参数说明通过 `.resx` 资源文件本地化：

- `Resources/Localization.resx` -- 中文（默认）
- `Resources/Localization.en.resx` -- 英文

分析器消息：

- `Resources/AnalyzerResources.resx` -- 中文（默认）
- `Resources/AnalyzerResources.en.resx` -- 英文
