using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Tex.Downloader;

internal class Program
{
    /// <summary>
    /// GitHub加速代理下载地址列表文本
    /// </summary>
    private const string UrlListSource = "https://gh-proxy.com/github.com/qiyuchenqq/Tex.Downloader/blob/main/downloadurl.txt";

    /// <summary>
    /// 保存路径：程序运行当前目录
    /// </summary>
    private static readonly string SaveDirectory = Environment.CurrentDirectory;

    /// <summary>
    /// 静态HttpClient单例，遵循.NET最佳实践
    /// </summary>
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    static async Task Main(string[] args)
    {
        Console.WriteLine("========================================");
        Console.WriteLine("          欢迎使用Tex.Downloader       ");
        Console.WriteLine("========================================");
        Console.WriteLine($"源地址文件：{UrlListSource}");
        Console.WriteLine($"文件保存目录：{SaveDirectory}\n");

        try
        {
            // 1. 获取远程txt文本内容
            string txtRaw = await GetRemoteTextAsync(UrlListSource);

            // 2. 解析所有有效下载链接
            List<string> downloadLinks = ParseDownloadLinks(txtRaw);

            if (downloadLinks.Count == 0)
            {
                Console.WriteLine("未读取到任何可下载链接，程序退出");
                PauseExit();
                return;
            }

            Console.WriteLine($"共解析到 {downloadLinks.Count} 个文件等待下载\n");

            // 3. 逐个串行下载
            for (int index = 0; index < downloadLinks.Count; index++)
            {
                string url = downloadLinks[index];
                Console.WriteLine($"[{index + 1}/{downloadLinks.Count}] 正在下载：{url}");
                await DownloadSingleFileAsync(url);
                Console.WriteLine($"[{index + 1}/{downloadLinks.Count}] 下载完成\n");
            }

            Console.WriteLine("下载完成！");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"运行异常：{ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Console.ResetColor();
        }

        PauseExit();
    }

    /// <summary>
    /// GET 请求获取远程纯文本
    /// </summary>
    private static async Task<string> GetRemoteTextAsync(string url)
    {
        HttpResponseMessage resp = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// 解析文本，过滤空行、#注释行，只保留http/https链接
    /// </summary>
    private static List<string> ParseDownloadLinks(string content)
    {
        List<string> result = new();
        string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            string trimLine = line.Trim();
            // 跳过注释、空行
            if (string.IsNullOrWhiteSpace(trimLine) || trimLine.StartsWith('#'))
                continue;
            // 校验合法网络链接
            if (trimLine.StartsWith("http://") || trimLine.StartsWith("https://"))
            {
                result.Add(trimLine);
            }
        }

        return result;
    }

    /// <summary>
    /// 单个文件流式下载到本地当前目录
    /// </summary>
    private static async Task DownloadSingleFileAsync(string fileUrl)
    {
        Uri uri = new(fileUrl);
        string fileName = Path.GetFileName(uri.LocalPath);

        // 无法提取文件名则使用GUID命名兜底
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = $"Tex_Download_{Guid.NewGuid():N}";
        }

        string fullPath = Path.Combine(SaveDirectory, fileName);

        // 文件已存在直接跳过
        if (File.Exists(fullPath))
        {
            Console.WriteLine($"文件 {fileName} 已存在，跳过下载");
            return;
        }

        // 流式下载，大文件不占用大量内存
        using HttpResponseMessage response = await _httpClient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        using Stream downloadStream = await response.Content.ReadAsStreamAsync();
        using FileStream fs = new(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        await downloadStream.CopyToAsync(fs);
    }

    /// <summary>
    /// 按任意键暂停退出
    /// </summary>
    private static void PauseExit()
    {
        Console.WriteLine("\n按任意键关闭窗口...");
        Console.ReadKey(true);
    }
}