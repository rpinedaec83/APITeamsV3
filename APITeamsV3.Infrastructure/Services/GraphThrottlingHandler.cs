using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace APITeamsV3.Infrastructure.Services
{
    /// <summary>
    /// Custom HTTP DelegatingHandler for Microsoft Graph API calls.
    /// Prevents 429 TooManyRequests throttling (e.g. "10Per10Secs" quota limit)
    /// by pacing requests and retrying up to 8 times with exponential backoff.
    /// </summary>
    public class GraphThrottlingHandler : DelegatingHandler
    {
        private readonly ILogger<GraphThrottlingHandler>? _logger;
        private const int MaxRetryAttempts = 8;
        private const int DefaultThrottleWaitMs = 10500;

        public GraphThrottlingHandler(ILogger<GraphThrottlingHandler>? logger = null)
        {
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Reactive retry logic ONLY when HTTP 429 (TooManyRequests) or 503 (ServiceUnavailable) occurs
            int attempt = 0;
            var currentRequest = request;

            while (true)
            {
                attempt++;
                HttpResponseMessage response;
                try
                {
                    response = await base.SendAsync(currentRequest, cancellationToken);
                }
                catch (Exception ex) when (attempt < MaxRetryAttempts)
                {
                    _logger?.LogWarning(ex, "[GraphThrottlingHandler] Exception sending Graph request (attempt {Attempt}/{Max}): {Message}", attempt, MaxRetryAttempts, ex.Message);
                    await Task.Delay(TimeSpan.FromSeconds(2 * attempt), cancellationToken);
                    currentRequest = await CloneHttpRequestMessageAsync(request);
                    continue;
                }

                if (response.StatusCode == (HttpStatusCode)429 || response.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    if (attempt >= MaxRetryAttempts)
                    {
                        _logger?.LogError("[GraphThrottlingHandler] Exceeded max retries ({Max}) on HTTP {StatusCode} for {Uri}", MaxRetryAttempts, (int)response.StatusCode, request.RequestUri);
                        return response;
                    }

                    int delayMs = GetRetryAfterMs(response, attempt);
                    _logger?.LogWarning("[GraphThrottlingHandler] HTTP 429/503 encountered for {Uri}. Waiting {DelayMs}ms before retry attempt {Attempt}/{Max}...", request.RequestUri, delayMs, attempt, MaxRetryAttempts);

                    await Task.Delay(delayMs, cancellationToken);
                    currentRequest = await CloneHttpRequestMessageAsync(request);
                    continue;
                }

                return response;
            }
        }

        private static int GetRetryAfterMs(HttpResponseMessage response, int attempt)
        {
            if (response.Headers.RetryAfter != null)
            {
                if (response.Headers.RetryAfter.Delta.HasValue)
                {
                    return (int)response.Headers.RetryAfter.Delta.Value.TotalMilliseconds + 500;
                }
                if (response.Headers.RetryAfter.Date.HasValue)
                {
                    var delta = response.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow;
                    if (delta.TotalMilliseconds > 0)
                    {
                        return (int)delta.TotalMilliseconds + 500;
                    }
                }
            }

            return Math.Min(DefaultThrottleWaitMs, (int)(1500 * Math.Pow(2, attempt - 1)));
        }

        private static string ExtractTenantOrHostKey(HttpRequestMessage request)
        {
            return request.RequestUri?.Host ?? "default";
        }

        private static async Task<HttpRequestMessage> CloneHttpRequestMessageAsync(HttpRequestMessage req)
        {
            var clone = new HttpRequestMessage(req.Method, req.RequestUri)
            {
                Version = req.Version
            };

            if (req.Content != null)
            {
                var ms = new System.IO.MemoryStream();
                await req.Content.CopyToAsync(ms);
                ms.Position = 0;

                var streamContent = new StreamContent(ms);
                foreach (var header in req.Content.Headers)
                {
                    streamContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
                clone.Content = streamContent;
            }

            foreach (var header in req.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            foreach (var prop in req.Options)
            {
                clone.Options.Set(new HttpRequestOptionsKey<object?>(prop.Key), prop.Value);
            }

            return clone;
        }
    }
}
