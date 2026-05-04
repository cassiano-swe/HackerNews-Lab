using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using System.Net;

namespace hacker.news.lab.infrastructure.resilience;

public static class ResiliencePolicies
{
    public static IAsyncPolicy<HttpResponseMessage> GetHttpPolicy()
    {
        var retry = HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
            );

        var circuitBreaker = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

        var timeout = Policy.TimeoutAsync<HttpResponseMessage>(5);

        return Policy.WrapAsync(retry, circuitBreaker, timeout);
    }
}