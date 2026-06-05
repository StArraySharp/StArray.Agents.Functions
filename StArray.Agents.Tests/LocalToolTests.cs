using Microsoft.Extensions.AI;
using StArray.Agents.Tools;

namespace StArray.Agents.Tests;

/// <summary>
/// 本地工具测试：验证 AgentToolAttribute 本地化、参数描述、纯函数逻辑。
/// Local tool tests: verify AgentToolAttribute localization, parameter descriptions, pure function logic.
/// 不需要 API Key，可直接运行。 / No API key needed, can run offline.
/// </summary>
public class LocalToolTests : TestBase
{
    [Test]
    public void TimeTools_GetNow_HasLocalizedDescription()
    {
        var timeTools = new TimeTools();
        var aiFunction = AIFunctionFactory.Create(timeTools.GetNow);
        TestContext.Progress.WriteLine(aiFunction.Description);
        Assert.That(aiFunction.Description, Is.EqualTo("获取当前本地时间"));
    }

    [Test]
    public void MathTools_GetTools_ReturnsAllFourOperations()
    {
        var mathTools = new MathTools();
        var functions = mathTools.GetTools();
        Assert.That(functions, Has.Count.EqualTo(4));
    }

    [Test]
    public void TextTools_GetTools_ReturnsAllFourTools()
    {
        var textTools = new TextTools();
        var functions = textTools.GetTools();
        Assert.That(functions, Has.Count.EqualTo(4));
    }

    [Test]
    public void FileTools_GetTools_ReturnsAllFiveTools()
    {
        var fileTools = new FileTools();
        var functions = fileTools.GetTools();
        Assert.That(functions, Has.Count.EqualTo(5));
    }

    [Test]
    public void CodeTools_GetTools_ReturnsAllTwoTools()
    {
        var codeTools = new CodeTools();
        var functions = codeTools.GetTools();
        Assert.That(functions, Has.Count.EqualTo(2));
    }

    [Test]
    public void TimeTools_GetTools_ReturnsAllThreeTools()
    {
        var timeTools = new TimeTools();
        var functions = timeTools.GetTools();
        Assert.That(functions, Has.Count.EqualTo(3));
    }

    [Test]
    public void MathTools_Add_ReturnsCorrectSum()
    {
        var mathTools = new MathTools();
        Assert.That(mathTools.Add(123, 456).Result, Is.EqualTo(579));
    }

    [Test]
    public void MathTools_Divide_ReturnsFailure_WhenDivideByZero()
    {
        var mathTools = new MathTools();
        var result = mathTools.Divide(10, 0);
        Assert.That(result.IsSuccess, Is.False);
    }

    [Test]
    public void TextTools_ToUpper_ReturnsUppercase()
    {
        var textTools = new TextTools();
        Assert.That(textTools.ToUpper("hello").Result, Is.EqualTo("HELLO"));
    }

    [Test]
    public void CodeTools_ValidateJson_AcceptsValidJson()
    {
        var codeTools = new CodeTools();
        var result = codeTools.ValidateJson("""{"key": "value"}""");
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Result, Does.Contain("有效"));
    }

    [Test]
    public void CodeTools_ValidateJson_RejectsInvalidJson()
    {
        var codeTools = new CodeTools();
        var result = codeTools.ValidateJson("{bad json");
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("无效"));
    }

    [Test]
    public void TimeTools_GetUtcNow_ReturnsISO8601Format()
    {
        var timeTools = new TimeTools();
        var result = timeTools.GetUtcNow();
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Result, Does.Contain("T").And.Contain("Z"));
    }
    
    [Test]
    public async Task WebTools_BingSearch_ReturnsValidResult()
    {
        var webTools = new WebTools();
        var result = await webTools.DuckDuckGoLiteSearch("OpenAI");
        TestContext.Progress.WriteLine(result.Result);
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Result, Does.Contain("OpenAI"));
    }
}
