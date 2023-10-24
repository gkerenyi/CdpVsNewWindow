using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CdpVsNewWindow
{
    public record AutoAttachedSession
    {
        [JsonPropertyName("sessionId")]
        public string SessionId { get; init; }

        [JsonPropertyName("targetInfo")]
        public TargetInfo TargetInfo { get; init; }

        [JsonPropertyName("waitingForDebugger")]
        public bool WaitingForDebugger { get; init; }
    }

    public record TargetInfo
    {
        [JsonPropertyName("attached")]
        public bool Attached { get; init; }

        [JsonPropertyName("browserContextId")]
        public string BrowserContextId { get; init; }

        [JsonPropertyName("canAccessOpener")]
        public bool CanAccessOpener { get; init; }

        [JsonPropertyName("targetId")]
        public string TargetId { get; init; }

        [JsonPropertyName("title")]
        public string Title { get; init; }

        [JsonPropertyName("type")]
        public string Type { get; init; }

        [JsonPropertyName("url")]
        public string Url { get; init; }
    }

    public record Request
    {
        [JsonPropertyName("headers")]
        public Dictionary<string, string> Headers { get; init; }

        [JsonPropertyName("initialPriority")]
        public string InitialPriority { get; init; }

        [JsonPropertyName("method")]
        public string Method { get; init; }

        [JsonPropertyName("referrerPolicy")]
        public string ReferrerPolicy { get; init; }

        [JsonPropertyName("url")]
        public Uri Url { get; init; }

        [JsonPropertyName("urlFragment")]
        public string UrlFragment { get; init; }

        [JsonPropertyName("mixedContentType")]
        public string MixedContentType { get; init; }

        [JsonPropertyName("isLinkPreload")]
        public bool? IsLinkPreload { get; init; }

        [JsonPropertyName("postData")]
        public string PostData { get; init; }

        [JsonPropertyName("hasPostData")]
        public bool? HasPostData { get; init; }
    }

    public record ResponseHeader
    {
        [JsonPropertyName("name")]
        public string Name { get; init; }

        [JsonPropertyName("value")]
        public string Value { get; init; }
    }

    public record RequestPausedEventArgs
    {
        [JsonPropertyName("frameId")]
        public string FrameId { get; init; }

        [JsonPropertyName("request")]
        public Request Request { get; init; }

        [JsonPropertyName("requestId")]
        public string RequestId { get; init; }

        [JsonPropertyName("resourceType")]
        public string ResourceType { get; init; }

        [JsonPropertyName("responseStatusCode")]
        public int? ResponseStatusCode { get; init; }

        [JsonPropertyName("responseHeaders")]
        public IReadOnlyList<ResponseHeader> ResponseHeaders { get; init; }

        [JsonPropertyName("networkId")]
        public string NetworkId { get; init; }
    }

}
