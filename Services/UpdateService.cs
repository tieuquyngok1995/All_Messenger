using All_in_One_Messenger.Models;
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace All_in_One_Messenger.Services;

public class UpdateService
{
    private readonly HttpClient _httpClient;
    private const string RepoApiUrl = "https://api.github.com/repos/tuanvq95/All-in-One_Messenger/releases/latest";

    public UpdateService()
    {
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// Send a request to retrieve the latest release, returning the raw data (tags, HTML URL, asset list).
    /// </summary>
    public async Task<ServiceResult<GitHubReleaseInfo>> GetLatestReleaseAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, RepoApiUrl);
            request.Headers.UserAgent.ParseAdd("AllInOneMessenger-UpdateChecker");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                return ServiceResult<GitHubReleaseInfo>.Fail(
                    $"GitHub trả về lỗi: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var info = new GitHubReleaseInfo
            {
                TagName = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? string.Empty : string.Empty,
                HtmlUrl = root.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() ?? string.Empty : string.Empty
            };

            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    info.Assets.Add(new GitHubReleaseAsset
                    {
                        Name = asset.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? string.Empty : string.Empty,
                        DownloadUrl = asset.TryGetProperty("browser_download_url", out var dlProp) ? dlProp.GetString() ?? string.Empty : string.Empty
                    });
                }
            }

            return ServiceResult<GitHubReleaseInfo>.Ok(info);
        }
        catch (HttpRequestException ex)
        {
            return ServiceResult<GitHubReleaseInfo>.Fail($"Lỗi kết nối mạng: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return ServiceResult<GitHubReleaseInfo>.Fail("Yêu cầu bị quá thời gian chờ (timeout).");
        }
        catch (JsonException ex)
        {
            return ServiceResult<GitHubReleaseInfo>.Fail($"Dữ liệu trả về từ GitHub không hợp lệ: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ServiceResult<GitHubReleaseInfo>.Fail($"Lỗi không xác định: {ex.Message}");
        }
    }

    /// <summary>
    /// Send a file download request to the specified path. The path/filename is determined by the calling party.
    /// </summary>
    public async Task<ServiceResult<string>> DownloadFileAsync(
        string url,
        string destinationPath,
        IProgress<double>? progress = null)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
            {
                return ServiceResult<string>.Fail(
                    $"Tải file thất bại: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var totalBytes = response.Content.Headers.ContentLength;

            using var httpStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = File.Create(destinationPath);

            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await httpStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalRead += bytesRead;

                if (totalBytes is > 0)
                {
                    var percent = (double)totalRead / totalBytes.Value * 100;
                    progress?.Report(percent);
                }
            }

            return ServiceResult<string>.Ok(destinationPath);
        }
        catch (HttpRequestException ex)
        {
            return ServiceResult<string>.Fail($"Lỗi kết nối mạng: {ex.Message}");
        }
        catch (IOException ex)
        {
            return ServiceResult<string>.Fail($"Lỗi ghi file vào ổ đĩa: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ServiceResult<string>.Fail($"Lỗi không xác định khi tải file: {ex.Message}");
        }
    }
}