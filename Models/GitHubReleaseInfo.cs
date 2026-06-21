using System.Collections.Generic;

namespace All_in_One_Messenger.Models;

public class GitHubReleaseAsset
{
    public string Name { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
}

public class GitHubReleaseInfo
{
    public string TagName { get; set; } = string.Empty;
    public string HtmlUrl { get; set; } = string.Empty;
    public List<GitHubReleaseAsset> Assets { get; set; } = new();
}

/// <summary>
/// The wrapper indicates the service call result: success + data, or failure + reason.
/// </summary>
public class ServiceResult<T>
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public T? Data { get; set; }

    public static ServiceResult<T> Ok(T data) => new() { Success = true, Data = data };
    public static ServiceResult<T> Fail(string error) => new() { Success = false, ErrorMessage = error };
}