# 智能体文件编辑测试指令

## 目标文件
`_AgentTestFiles/CrazyComplexCSharpFile.cs`

## 说明
请逐条完成以下编辑任务。每条指令需独立执行，完成后检查是否修改正确。

---

### 任务 1：重命名泛型约束
在 `IEntity<out TKey>` 接口的约束中，将 `IParsable<TKey>` 改为 `ISpanParsable<TKey>`。  
需修改所有相关接口和类（约 20+ 处）。

### 任务 2：修改 record 结构
在 `Money` record struct 的构造函数中，将 `CurrencyCode.XXX` 的异常判断改为允许 `XXX` 作为 "未设置" 状态（允许 Amount 为 0）。

### 任务 3：新增枚举成员
在 `CurrencyCode` 枚举中新增：
- `KRW = 410` — South Korean Won
- `SGD = 702` — Singapore Dollar  
- `TWD = 901` — New Taiwan Dollar

### 任务 4：重命名方法
在 `UserAccount` 类中，将 `SetPassword` 改名为 `ChangePassword`，同步更新所有调用处逻辑。

### 任务 5：提取基类
将 `UserAccount` 中的 `IsDirty` 属性和相关逻辑（`_isDirty` 字段、`protected set`）移到 `EntityBase<TKey>` 中（已在基类中存在，需统一实现方式：移除子类重复定义）。

### 任务 6：修改模式匹配
在 `PatternMatcher.DescribeEntity` 中，增加对 `UserAccount { Status: AccountStatus.PendingActivation }` 的处理，返回 `"User '{username}' (pending activation)"`。

### 任务 7：拆分大类
将 `HybridCacheProvider` 中的 `GetAsync` / `SetAsync` / `RemoveAsync` 三个方法提取到 `ICacheProvider` 的默认实现方法中。

### 任务 8：修改扩展方法
在 `StringExtensions.ToCamelCase` 中，增加对下划线命名 `snake_case` 的支持（转换为 camelCase，去掉下划线并将后一个字母大写）。

### 任务 9：添加参数验证
在 `InMemoryRepository<TEntity, TKey>.UpdateAsync` 方法中，先用 `ObjectDisposedException` 检查 `_disposed` 状态。

### 任务 10：重命名常量字段
将 `AgentConfiguration.Default` 及所有对它的引用改为 `AgentConfiguration.DefaultConfig`。

### 任务 11：修改属性定义
将 `FileAccessPermissions` 枚举中的 `Description` 属性值改为英文（如 `"Owner Full Control"`）。

### 任务 12：移动 partial class 成员
将 `UserAccount` 的 `partial class` 中的嵌套 record `UserPreferences` 移动到单独的文件（模拟操作：在当前文件夹内标注其应该被移出，标注 `// TODO: MOVE_TO_SEPARATE_FILE`）。

### 任务 13：修改 LINQ 查询
在 `DataTransformer.GroupAndAggregate` 中，将 `ToImmutableDictionary` 改为 `ToFrozenDictionary`，命名字典为 `frozenGroups`。

### 任务 14：添加异常过滤器
在 `ParallelTaskExecutor.ExecuteAsync` 的 `catch` 块改为使用异常过滤器 `when (ex is not OperationCanceledException)`。

### 任务 15：修改 async 签名
在 `DataStreamProcessor` 中，将 `AddStage<T>()` 方法的返回类型从 `DataStreamProcessor` 改为 `DataStreamProcessor<TStage>`。

### 任务 16：替换字符串常量
将文件中所有 `"Light"` 字符串替换为引用 `ThemeMode.Light`（假设已有一个对应枚举需要加在文件末尾附近）。

### 任务 17：修改 XML 注释
将文件中所有形如 `<see cref="..." />` 的 cref 属性从 `UserAccount` 改为 `UserAccount` 的完全限定名 `StArray.TestTools.Entities.UserAccount`。

### 任务 18：解构嵌套 using
将文件顶部的 `using System.Text.Json;` 和 `using System.Text.Json.Serialization;` 合并为 `using System.Text.Json;` 和 `using System.Text.Json.Serialization;` 的去重（合并时保留公共部分）。

### 任务 19：修改 unsafe 代码
在 `MemoryHelper.CompareMemory` 中，将 `Avx2.IsSupported` 检查改为 `Avx2.IsSupported && Vector256.IsHardwareAccelerated`。

### 任务 20：添加 AOT 兼容特性
在所有 record struct 前添加 `[RequiresDynamicCode("可能会导致 AOT 兼容性问题")]` 条件编译特性（仅在 `#if !AOT` 下）。

---

## 验收标准
- 所有修改后文件应保持语法正确性（可被 IDE 解析）
- 命名一致性保持
- 不改动逻辑语义（除非明确要求）
- 注释至少更新相关部分
