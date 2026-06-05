using System.Text.RegularExpressions;
using StArray.Agents.Abstraction;
using StArray.Agents.Annotations;
using StArray.Agents.Core;

namespace StArray.Agents.Tools;

/// <summary>
/// 文件工具集：提供文件存在检查、读写操作。
/// File tools: provides file existence check, read, and write operations.
/// </summary>
public partial class FileTools : ITools
{
    private const string RES_FILE_PATH_KEY = "Tool_File_FilePath";
    private const string RES_DIRECTORY_PATH_KEY = "Tool_File_DirectoryPath";
    [AgentTool("Tool_File_Exists")]
    public ToolResponse<bool> Exists(
        [ToolParameter(RES_FILE_PATH_KEY)] string path)
    {
        return ToolResponse<bool>.Success(File.Exists(path));
    }
    
    [AgentTool("Tool_File_ListFiles")]
    public ToolResponse<string[]> ListFiles(
        [ToolParameter(RES_DIRECTORY_PATH_KEY)] string path,
        [ToolParameter("Tool_File_ListFiles_recursive")] bool recursive = false)
    {
        return ToolResponse<string[]>.Success(recursive ? Directory.GetFiles(path, "*", SearchOption.AllDirectories) : Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly));
    }

    [AgentTool("Tool_File_ReadAllText")]
    public ToolResponse<string> ReadAllText(
        [ToolParameter(RES_FILE_PATH_KEY)] string path)
    {
        try
        {
            return ToolResponse<string>.Success(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            return ToolResponse<string>.Failure($"读取文件失败：{ex.Message}");
        }
    }

    [AgentTool("Tool_File_WriteAllText")]
    public ToolResponse<string> WriteAllText(
        [ToolParameter(RES_FILE_PATH_KEY)] string path,
        [ToolParameter("Tool_File_WriteAllText_content")] string content)
    {
        try
        {
            File.WriteAllText(path, content);
            return ToolResponse<string>.Success("文件写入成功。");
        }
        catch (Exception ex)
        {
            return ToolResponse<string>.Failure($"写入文件失败：{ex.Message}");
        }
    }

    [AgentTool("Tool_FileTools_ReadFileContent")]
    public ToolResponse<string> ReadFileContent([ToolParameter(RES_FILE_PATH_KEY)] string path,
        [ToolParameter("Tool_FileTools_ReadFileContent_startLine")] int startLine,
        [ToolParameter("Tool_FileTools_ReadFileContent_rangeLines")] int rangeLines)
    {
        try
        {
            using StreamReader reader = new(path);
            List<string> lines = new();
            int currentLine = 0;
            while (!reader.EndOfStream)
            {
                string line = reader.ReadLine() ?? string.Empty;
                if (currentLine >= startLine && currentLine < startLine + rangeLines)
                {
                    lines.Add(line);
                }
                currentLine++;
            }
            return ToolResponse<string>.Success(string.Join("\n", lines));
        } catch (Exception e) {
            return ToolResponse<string>.Failure($"读取文件内容失败：{e.Message}");
        }
    }
    
    [AgentTool("Tool_File_ReplaceStringInFile")]
    public ToolResponse<string> ReplaceStringInFile([ToolParameter(RES_FILE_PATH_KEY)] string path,
        [ToolParameter("Tool_File_ReplaceStringInFile_oldValue")] string oldValue,
        [ToolParameter("Tool_File_ReplaceStringInFile_newValue")] string newValue,
        [ToolParameter("Tool_File_ReplaceStringInFile_isRegex")] bool isRegex = false)
    {
        try
        {
            string content = File.ReadAllText(path);
            if (isRegex) {
                content = Regex.Replace(content, oldValue, newValue);
            } else {
                content = content.Replace(oldValue, newValue);
            }
            File.WriteAllText(path, content);
            return ToolResponse<string>.Success("替换成功。");
        } catch (Exception e) {
            return ToolResponse<string>.Failure($"替换文件内容失败：{e.Message}");
        }
    }

    [AgentTool("Tool_File_Delete")]
    public ToolResponse<string> Delete([ToolParameter("Tool_File_Delete_path")] string path)
    {
        try
        {
            File.Delete(path);
            return ToolResponse<string>.Success("删除成功。");
        }
        catch (Exception e)
        {
            return ToolResponse<string>.Failure($"删除文件失败：{e.Message}");
        }
    }
    
    [AgentTool("Tool_File_SearchContentInFiles")]
    public ToolResponse<string> SearchContentInFiles([ToolParameter("Tool_File_SearchContentInFiles_paths")] string[] paths,
        [ToolParameter("Tool_File_SearchContentInFiles_content")] string content,
        [ToolParameter("Tool_File_SearchContentInFiles_isRegex")] bool isRegex = false)
    { 
        try
        {
            List<string> result = new();
            foreach (string path in paths)
            {
                if (!File.Exists(path))
                {
                    continue;
                }
                int lineNumber = 0;
                using StreamReader reader = new(path);
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine() ?? string.Empty;
                    if (isRegex)
                    {
                        if (Regex.IsMatch(line, content))
                        {
                            result.Add($"{path}:{lineNumber}->{line}");
                        }
                    }
                    else
                    {
                        if (line.Contains(content))
                        {
                            result.Add($"{path}:{lineNumber}->{line}");
                        }
                    }
                    lineNumber++;
                }
            }
            return ToolResponse<string>.Success(string.Join("\n", result));
        } catch (Exception e)
        {
            return ToolResponse<string>.Failure($"搜索文件内容失败：{e.Message}");
        }
    }
    
    [AgentTool("Tool_File_GetCurrentDirectory")]
    public ToolResponse<string> GetCurrentDirectory()
    {
        return ToolResponse<string>.Success(Directory.GetCurrentDirectory());
    }
    
    [AgentTool("Tool_File_ChangeWorkingDirectory")]
    public ToolResponse<string> ChangeWorkingDirectory(
        [ToolParameter(RES_DIRECTORY_PATH_KEY)] string path)
    {
        try
        {
            Directory.SetCurrentDirectory(path);
            return ToolResponse<string>.Success("更改工作目录成功。");
        } catch (Exception e)
        {
            return ToolResponse<string>.Failure($"更改工作目录失败：{e.Message}");
        }
    }
}