using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ShareX.Linux.Core.Uploaders;

public class UploadResult
{
    public bool Success { get; set; }
    public string? Url { get; set; }
    public string? DeletionUrl { get; set; }
    public string? ErrorMessage { get; set; }
    public string? RawResponse { get; set; }

    public static UploadResult Failed(string error) => new() { Success = false, ErrorMessage = error };
    public static UploadResult Succeeded(string url, string? deletionUrl = null, string? rawResponse = null) =>
        new() { Success = true, Url = url, DeletionUrl = deletionUrl, RawResponse = rawResponse };
}

public interface IUploadService
{
    string ServiceName { get; }
    Task<UploadResult> UploadAsync(Stream stream, string fileName, CancellationToken cancellationToken = default);
}
