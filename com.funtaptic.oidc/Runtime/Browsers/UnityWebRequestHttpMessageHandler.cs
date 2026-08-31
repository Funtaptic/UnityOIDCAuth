using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Funtaptic.OIDC.WebGL
{
    /// <summary>
    /// HttpMessageHandler backed by UnityWebRequest for browser builds.
    /// WebGL cannot use the default System.Net.Http transport because it has
    /// no direct socket access; UnityWebRequest delegates the request to the
    /// browser's Fetch/XMLHttpRequest implementation instead.
    /// </summary>
    internal sealed class UnityWebRequestHttpMessageHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var body = request.Content == null
                ? null
                : await request.Content.ReadAsByteArrayAsync();

            await Awaitable.MainThreadAsync();
            cancellationToken.ThrowIfCancellationRequested();

            Debug.Log($"[OIDC WebGL] HTTP {request.Method} request started: {DescribeUri(request.RequestUri)}.");
            using var unityRequest = CreateUnityWebRequest(request, body);
            var operation = unityRequest.SendWebRequest();

            while (!operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Awaitable.NextFrameAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (unityRequest.result == UnityWebRequest.Result.ConnectionError ||
                unityRequest.result == UnityWebRequest.Result.DataProcessingError)
            {
                Debug.LogError($"[OIDC WebGL] HTTP request failed: {DescribeUri(request.RequestUri)}; Result: {unityRequest.result}; Error: {unityRequest.error ?? "none"}.");
                throw new HttpRequestException(
                    $"OIDC request failed for {request.RequestUri}: {unityRequest.error}");
            }

            if (unityRequest.responseCode >= 400)
                Debug.LogWarning($"[OIDC WebGL] HTTP request returned {(long)unityRequest.responseCode}: {DescribeUri(request.RequestUri)}.");
            else
                Debug.Log($"[OIDC WebGL] HTTP request completed with {(long)unityRequest.responseCode}: {DescribeUri(request.RequestUri)}.");

            var response = new HttpResponseMessage((HttpStatusCode)unityRequest.responseCode)
            {
                RequestMessage = request,
                ReasonPhrase = unityRequest.error
            };

            var responseBytes = unityRequest.downloadHandler?.data ?? Array.Empty<byte>();
            response.Content = new ByteArrayContent(responseBytes);

            var responseHeaders = unityRequest.GetResponseHeaders();
            if (responseHeaders != null)
            {
                foreach (var header in responseHeaders)
                {
                    if (!response.Headers.TryAddWithoutValidation(header.Key, header.Value))
                        response.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            return response;
        }

        private static UnityWebRequest CreateUnityWebRequest(HttpRequestMessage request, byte[] body)
        {
            var unityRequest = new UnityWebRequest(request.RequestUri, request.Method.Method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 30
            };

            try
            {
                if (body != null)
                    unityRequest.uploadHandler = new UploadHandlerRaw(body);

                CopyHeaders(unityRequest, request);
                return unityRequest;
            }
            catch
            {
                unityRequest.Dispose();
                throw;
            }
        }

        private static void CopyHeaders(UnityWebRequest unityRequest, HttpRequestMessage request)
        {
            foreach (var header in request.Headers)
                SetHeader(unityRequest, header.Key, header.Value);

            if (request.Content == null)
                return;

            foreach (var header in request.Content.Headers)
                SetHeader(unityRequest, header.Key, header.Value);
        }

        private static void SetHeader(
            UnityWebRequest unityRequest,
            string name,
            System.Collections.Generic.IEnumerable<string> values)
        {
            // Unity calculates this header from the upload handler and rejects
            // attempts to set it explicitly.
            if (string.Equals(name, "Content-Length", StringComparison.OrdinalIgnoreCase))
                return;

            unityRequest.SetRequestHeader(name, string.Join(", ", values));
        }

        private static string DescribeUri(Uri uri)
        {
            if (uri == null)
                return "<null>";

            return $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";
        }
    }
}
