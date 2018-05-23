using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MilestoneTG.Splunk.FailureInjectionTesting
{
    /// <summary>
    /// Introduces Chaos testing into the underlying HttpClient
    /// </summary>
    /// <seealso cref="System.Net.Http.DelegatingHandler" />
    public class SplunkFitDelegatingHandler : DelegatingHandler
    {
        /// <summary>
        /// Initializes a new instance of <see cref="SplunkFitDelegatingHandler"/>
        /// </summary>
        /// <param name="innerHander"></param>
        public SplunkFitDelegatingHandler(HttpMessageHandler innerHander) 
            :base(innerHander)
        {

        }

        /// <summary>
        /// Specifies a mock hook to use instead of SendAsync().
        /// </summary>
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> InjectFailure { get; set; }

        /// <summary>
        /// Sends an HTTP request to the inner handler to send to the server as an asynchronous operation.
        /// </summary>
        /// <param name="request">The HTTP request message to send to the server.</param>
        /// <param name="cancellationToken">A cancellation token to cancel operation.</param>
        /// <returns>
        /// Returns <see cref="T:System.Threading.Tasks.Task`1" />. The task object representing the asynchronous operation.
        /// </returns>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (InjectFailure != null)
                return await InjectFailure(request, cancellationToken).ConfigureAwait(false);

            if (ChaosConfig.Enabled)
            {
                switch (ChaosConfig.FailureType)
                {
                    case FailureType.HttpStatusCode:
                        return new HttpResponseMessage((HttpStatusCode)ChaosConfig.Value);
                    case FailureType.Latency:
                        await Task.Delay(ChaosConfig.Value).ConfigureAwait(false);
                        break;
                    case FailureType.Random:
                        HttpResponseMessage chaosResponse = GetFromMs();
                        if (chaosResponse != null)
                            return chaosResponse;
                        break;
                }

            }

            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        private HttpResponseMessage GetRandom()
        {
            var rnd = new Random();
            return KnownResponses.GetHttpResponseMessageForCode(rnd.Next(1, KnownResponses.SplunkRepsonses.Length));
        }

        private HttpResponseMessage GetFromMs()
        {
            int ms = DateTime.Now.Millisecond;
            if (ms >= KnownResponses.SplunkRepsonses.Length && ms > 99)
                ms -= (int)Math.Round((double)ms, -2);

            if (ms >= KnownResponses.SplunkRepsonses.Length)
                ms -= (int)Math.Round((double)ms, -1);

            return KnownResponses.GetHttpResponseMessageForCode(ms);
        }
    }
}