using StArray.Agents.Abstraction;
using StArray.Agents.Annotations;
using StArray.Agents.Core;

namespace StArray.Agents.Tools;

public partial class WebTools : ITools
{
    private readonly HttpClient _httpClient;
    
    public WebTools()
    {
        // 创建自定义的 HttpClientHandler 来启用自动解压
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | 
                                     System.Net.DecompressionMethods.Deflate | 
                                     System.Net.DecompressionMethods.Brotli
        };
        _httpClient = new HttpClient(handler);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    [AgentTool("Tool_Web_WebFetch")]
    public async Task<ToolResponse<string>> WebFetch(
        [ToolParameter("Tool_Web_WebFetch_url")] string url,
        [ToolParameter("Tool_Web_WebFetch_headers")] Dictionary<string, string>? customHeaders = null)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            
            // 添加默认请求头，模拟真实浏览器
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0");
            request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            request.Headers.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,en;q=0.8");
            request.Headers.AcceptEncoding.ParseAdd("gzip, deflate, br");
            request.Headers.Connection.ParseAdd("keep-alive");
            request.Headers.Upgrade.ParseAdd("1");
            
            // 添加自定义请求头
            if (customHeaders != null)
            {
                foreach (var header in customHeaders)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
            
            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                return ToolResponse<string>.Success(content);
            }
            else
            {
                return ToolResponse<string>.Failure($"HTTP {response.StatusCode}: {content.Substring(0, Math.Min(100, content.Length))}");
            }
        }
        catch (HttpRequestException ex)
        {
            return ToolResponse<string>.Failure($"网络请求失败: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return ToolResponse<string>.Failure("请求超时");
        }
        catch (Exception ex)
        {
            return ToolResponse<string>.Failure($"未知错误: {ex.Message}");
        }
    }

    [AgentTool("Tool_Web_DuckDuckGoLite")]
    public async Task<ToolResponse<string>> DuckDuckGoLiteSearch(
        [ToolParameter("Tool_Web_DuckDuckGoLite_query")] string query)
    {
        var headers = GetDuckDuckGoHeaders();
        var encodedQuery = Uri.EscapeDataString(query);
        var url = $"https://lite.duckduckgo.com/lite/?q={encodedQuery}";
    
        return await WebFetch(url, headers);
    }

    private Dictionary<string, string> GetDuckDuckGoHeaders()
    {
        return new Dictionary<string, string>
        {
            { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/148.0.0.0 Safari/537.36 Edg/148.0.0.0" },
            { "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8" },
            { "Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8" },
            { "Accept-Encoding", "gzip, deflate, br" },
            { "Connection", "keep-alive" },
            { "Upgrade-Insecure-Requests", "1" },
            // DuckDuckGo 会检查 Referer
            { "Referer", "https://duckduckgo.com/" }
        };
    }
}