using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ShareX.Linux.Core.Uploaders;

public class ImgurUploader : IUploadService
{
    public string ServiceName => "Imgur";

    // Standard ShareX Imgur Anonymous Client ID
    private const string DefaultClientId = "9fe5f29d288d011";
    private readonly string _clientId;
    private static readonly HttpClient _httpClient = new();

    public ImgurUploader(string? customClientId = null)
    {
        _clientId = string.IsNullOrWhiteSpace(customClientId) ? DefaultClientId : customClientId;
    }

    public async Task<UploadResult> UploadAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(stream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(streamContent, "image", fileName);

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.imgur.com/3/image")
            {
                Content = content
            };
            request.Headers.TryAddWithoutValidation("Authorization", $"Client-ID {_clientId}");

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return UploadResult.Failed($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {responseString}");
            }

            var json = JObject.Parse(responseString);
            var success = json["success"]?.Value<bool>() ?? false;
            if (!success)
            {
                var error = json["data"]?["error"]?.ToString() ?? "Unknown Imgur error";
                return UploadResult.Failed(error);
            }

            var link = json["data"]?["link"]?.ToString();
            var deleteHash = json["data"]?["deletehash"]?.ToString();
            string? deletionUrl = !string.IsNullOrEmpty(deleteHash) ? $"https://imgur.com/delete/{deleteHash}" : null;

            if (string.IsNullOrEmpty(link))
            {
                return UploadResult.Failed("Imgur returned empty link");
            }

            return UploadResult.Succeeded(link, deletionUrl, responseString);
        }
        catch (Exception ex)
        {
            return UploadResult.Failed($"Imgur upload exception: {ex.Message}");
        }
    }
}
