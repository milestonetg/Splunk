using MilestoneTG.Splunk.FailureInjectionTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MilestoneTG.TransientFaultHandling;
using MilestoneTG.TransientFaultHandling.Http;

namespace MilestoneTG.Splunk
{
    /// <summary>
    /// Sends a compressed json splunk payload to Splunk. This class uses
    /// HttpClient under the hood, and as such, should be loaded as a Singleton.
    /// The implementation employs GZip compression, Transient Fault Handling, Circuit Breaker, and Bulkhead Patterns.
    /// </summary>
    /// <remarks>
    /// ### Bulkhead Pattern
    ///  
    /// The client uses the bulkhead pattern to help ensure the client doesn't storm Splunk and cause rate limiting
    /// to kick in. This is a static setting, so it applies across all logger instances.
    /// 
    /// See also: https://docs.microsoft.com/en-us/azure/architecture/patterns/bulkhead
    ///
    /// ### Circuit Breaker Pattern
    /// 
    /// The client uses the circuit breaker pattern to stop sending logs to splunk for a configurable time window in the 
    /// event of a failure with Splunk. A single failure, unresolved by the transient fault handling, will open (trip)
    /// the circuit breaker. The CircuitBreaker state is static, so if one logger instance trips the breaker,
    /// all loggers in the same AppDomain will respect that the breaker has been tripped. When the window has expired,
    /// a single log write will be attempted. If it is successful, the circuit breaker is closed again.
    /// 
    /// The circuit breaker opens on a single failure rather than some failure rate because odds are, if the failure was
    /// not resolved by transient fault handling, then there is a more signigicant issue communicating with Splunk and
    /// subsequent attempts would likely fail anyway. A retry queue is available, and
    /// a fallback can be provided. There is no significant application risk in simply tripping the circuit and trying
    /// again after the expirey window.
    /// 
    /// Short circuited writes to Splunk--write attempts while the circuit breaker is open--will be pushed to an
    /// in-memory retry queue. When the circuit is closed again, the retry queue will be drained and the entries written
    /// to Splunk.
    /// 
    /// When the CircuitBreakerState changes, the <see cref="CircuitBreakerStateChanged"/> event is raised.
    /// 
    /// See also: https://docs.microsoft.com/en-us/azure/architecture/patterns/circuit-breaker
    /// 
    /// ### Errors and Fallback
    /// 
    /// Any errors while writing to Splunk are written to a TraceListener using Trace.TraceError,
    /// indicating the error that occured as well as the original message that was trying to be
    /// sent.
    ///
    /// Any transient faults are written to a TraceListener using Trace.TraceWarning. Retry attemps on those faults 
    /// raise the <see cref="SplunkHttpCollectorClient.Retrying"/> event.
    ///
    /// If you would like to capture logging errors, you can implement one or more 
    /// <see cref="System.Diagnostics.TraceListener"/>s to write the errors to a destination of your choosing.  
    /// For example, on Windows, you could add an EventLogTraceListener to write the errors to the event log.
    ///
    /// Provide a fallback action to write the payload to a different logging provider. A SplunkResponse with an error
    /// code of `MonitoringEventIds.DependencyServiceFailure.Id` will always be returned when the fallback 
    /// action is used.  Messages are written to the <see cref="TraceListener"/> if the fallback fails.
    /// </remarks>
    public sealed class SplunkHttpCollectorClient : IDisposable
    {
        public static class EventIds
        {
            /// <summary>
            /// Used by log entries that trace calls to downstream services.
            /// </summary>
            /// <value>52000</value>
            public const int DependencyServiceCallTrace = 52000;

            /// <summary>
            /// A transient fault was detected when calling downstream servcices.
            /// </summary>
            /// <value>52001</value>
            public const int DependencyServiceFault = 52001;

            /// <summary>
            /// A failure occured when calling downstream services.
            /// </summary>
            /// <value>52002</value>
            public const int DependencyServiceFailure = 52002;

            /// <summary>
            /// Slow response time when calling a downstream service.
            /// </summary>
            /// <value>52003</value>
            public const int SlowDependencyResponseTime = 52003;

            /// <summary>
            /// A Request trace.
            /// </summary>
            /// <value>53000</value>
            public const int RequestTrace = 53000;

            /// <summary>
            /// Used to identify slow server operations.
            /// </summary>
            /// <value>53001</value>
            public const int SlowServiceResponseTime = 53001;

            /// <summary>
            /// An error occured in the operation.
            /// </summary>
            /// <value>55000</value>
            public const int ServiceError = 55000;
        }

        /// <summary>
        /// Fires when a call to Splunk is retrying as a result of a transient fault.
        /// </summary>
        public event EventHandler<RetryingEventArgs> Retrying;

        /// <summary>
        /// Fires when the CircuitBreakerState changes. The event arguments provide the state of the circuit breaker
        /// when the event fired.
        /// </summary>
        public event EventHandler<CircuitBreakerState> CircuitBreakerStateChanged;

        public static class Metrics
        {
            /// <summary>
            /// The number of transient faults when communicating with Splunk since the application was started.
            /// </summary>
            public static long TransientFaultCount => Interlocked.Read(ref _transientFaultCounter);
            static long _transientFaultCounter = 0;

            /// <summary>
            /// The number of failures when communicating with Splunk since the application was started.
            /// </summary>
            public static long FailureCount => Interlocked.Read(ref _failureCounter);
            static long _failureCounter = 0;

            public static long RequestCount => Interlocked.Read(ref _requestCounter);
            static long _requestCounter = 0;

            public static long SuccessCount => Interlocked.Read(ref _successCounter);
            static long _successCounter = 0;

            public static long LastExectionTime => Interlocked.Read(ref _lastExectionTime);
            static long _lastExectionTime = 0;
            public static long MinExectionTime => Interlocked.Read(ref _minExectionTime);
            static long _minExectionTime = 0;
            public static long AvgExectionTime => Interlocked.Read(ref _avgExectionTime);
            static long _avgExectionTime = 0;
            public static long maxExectionTime => Interlocked.Read(ref _maxExectionTime);
            static long _maxExectionTime = 0;

            public static void IncrementRequestCount()
            {
                Interlocked.Increment(ref _requestCounter);
            }

            public static void IncrementSuccessCount()
            {
                Interlocked.Increment(ref _successCounter);
            }

            public static void IncrementFailureCount()
            {
                Interlocked.Increment(ref _failureCounter);
            }

            public static void IncrementTransientFaultCount()
            {
                Interlocked.Increment(ref _transientFaultCounter);
            }

            public static void MeasureExectutionTime(long value)
            {
                Interlocked.Exchange(ref _lastExectionTime, value);

                if (value < MinExectionTime || MinExectionTime == 0)
                    Interlocked.Exchange(ref _minExectionTime, value);

                if (value > maxExectionTime)
                    Interlocked.Exchange(ref _maxExectionTime, value);

                long oldSum = AvgExectionTime * (RequestCount - 1);
                long newSum = oldSum + value;
                long newAvg = newSum / RequestCount;
                Interlocked.Exchange(ref _avgExectionTime, newAvg);
            }

            public static void Reset()
            {
                Interlocked.Exchange(ref _transientFaultCounter, 0);
                Interlocked.Exchange(ref _failureCounter, 0);
                Interlocked.Exchange(ref _requestCounter, 0);
                Interlocked.Exchange(ref _successCounter, 0);
                Interlocked.Exchange(ref _lastExectionTime, 0);
                Interlocked.Exchange(ref _minExectionTime, 0);
                Interlocked.Exchange(ref _avgExectionTime, 0);
                Interlocked.Exchange(ref _maxExectionTime, 0);
            }
        }

        /// <summary>
        /// Current state of the CircuitBreaker.
        /// </summary>
        public static CircuitBreakerState CircuitBreakerState => (CircuitBreakerState)_circuitBreakerState;
        static int _circuitBreakerState = 0;
        static long _circuitBreakerOpenTime = -1;

        //used for bulkheading. Helps ensures that we don't storm Splunk.
        static SemaphoreSlim _bulkheadSemaphore = new SemaphoreSlim(10);


        HttpClient httpClient;
        SplunkHttpCollectorClientOptions options;
        Action<object> fallback;
        SplunkFitDelegatingHandler chaosHandler;
        ConcurrentQueue<string> retryQueue = new ConcurrentQueue<string>();

        /// <summary>
        /// Creates or returns an existing instance of <see cref="SplunkHttpCollectorClient"/>.
        /// </summary>
        /// <remarks>
        /// If an instance already exists, the new options passed in will be used for that instance. This allows
        /// applications to change options like the CircuitBreakerRetryWindow and Timeout on the fly. 
        /// However, changing the Key or Url will have no impact.
        /// </remarks>
        /// <param name="options"></param>
        /// <param name="retryPolicy"></param>
        /// <param name="fallback">Action to perform if Splunk is unavailable.</param>
        /// <returns>A singleton instance of <see cref="SplunkHttpCollectorClient"/></returns>
        public static SplunkHttpCollectorClient GetInstance(SplunkHttpCollectorClientOptions options, RetryPolicy retryPolicy = null, Action<object> fallback = null)
        {
            if (_instance == null)
            {
                _instance = new SplunkHttpCollectorClient(options, retryPolicy, fallback);
            }
            else
            {
                _instance.options = options;
                _instance.httpClient.Timeout = options.Timeout;
            }

            return _instance;
        }
        static SplunkHttpCollectorClient _instance;

        /// <summary>
        /// Initializes an instance of <see cref="SplunkHttpCollectorClient"/> with the specified <see cref="SplunkHttpCollectorClientOptions"/>
        /// and optional <see cref="RetryPolicy"/>.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="retryPolicy"></param>
        /// <param name="fallback">Action to perform if Splunk is unavailable.</param>
        private SplunkHttpCollectorClient(SplunkHttpCollectorClientOptions options, RetryPolicy retryPolicy = null, Action<object> fallback = null)
        {
            this.options = options;
            this.fallback = fallback;

            HttpClientHandler httpClientHandler = new HttpClientHandler
            {
                //Decompress the response
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            //Failure Injection Testing
            chaosHandler = new SplunkFitDelegatingHandler(httpClientHandler);

            //Transient Fault Handling
            RetryDelegatingHandler retryHandler = new RetryDelegatingHandler(chaosHandler);

            if (retryPolicy != null)
                retryHandler.RetryPolicy = retryPolicy;
            
            retryHandler.RetryPolicy.Retrying += OnTransientRetry;

            //Compress requests
            CompressionDelegatingHandler commpressionHandler = new CompressionDelegatingHandler(retryHandler);

            httpClient = new HttpClient(commpressionHandler);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Splunk", options.ApiKey);
            httpClient.Timeout = options.Timeout;
        }
        
        /// <summary>
        /// Defines a mock hook for testing.
        /// </summary>
        /// <remarks>
        /// This sets the Mock property of the underlying <see cref="SplunkFitDelegatingHandler"/>. The handler is
        /// added as the very last handler in the pipeline, so defining the Mock allows failuret esting of Splunk and
        /// this client's, as well as your code's, ability to handle that failure gracefully.
        /// </remarks>
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> InjectFailure
        {
            get => chaosHandler.InjectFailure;
            set => chaosHandler.InjectFailure = value;
        }

        /// <summary>
        /// Closes the CircuitBreaker and resets all counters.
        /// </summary>
        public void Reset()
        {
            Interlocked.Exchange(ref _circuitBreakerOpenTime, -1);
            Interlocked.Exchange(ref _circuitBreakerState, CircuitBreakerStateConstants.Closed);
            Metrics.Reset();
        }

        /// <summary>
        /// Sends the provided SplunkEventMetadata to Splunk.
        /// </summary>
        /// <param name="splunkEvent"></param>
        /// <returns>The deserialized resonse from Splunk.</returns>
        /// <exception cref="ArgumentNullException">If splunkEvent is null.</exception>
        /// <exception cref="JsonException">If the payload cannot be serialized into json.</exception>
        /// <exception cref="FormatException">If the payload is too large and can't be split into valid ingestable chuncks.</exception>
        public SplunkResponse SendToSplunk(SplunkEventMetadata splunkEvent)
        {
            if (splunkEvent == null)
                throw new ArgumentNullException(nameof(splunkEvent));
            
            //Microsoft's pattern for preventing deadlocks
            return Task.Run(() => SendToSplunkAsync(splunkEvent))
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        /// <summary>
        /// Sends the provided SplunkEventMetadata to Splunk.
        /// </summary>
        /// <param name="splunkEvent"></param>
        /// <returns>The deserialized resonse from Splunk.</returns>
        /// <exception cref="ArgumentNullException">If splunkEvent is null.</exception>
        /// <exception cref="JsonException">If the payload cannot be serialized into json.</exception>
        /// <exception cref="FormatException">If the payload is too large and can't be split into valid ingestable chuncks.</exception>
        public Task<SplunkResponse> SendToSplunkAsync(SplunkEventMetadata splunkEvent)
        {
            if (splunkEvent == null)
                throw new ArgumentNullException(nameof(splunkEvent));

            string json = JsonConvert.SerializeObject(splunkEvent);
            return SendToSplunkAsync(json);
        }

        /// <summary>
        /// Sends the provided SplunkEventMetadata to Splunk.
        /// </summary>
        /// <remarks>
        /// Splunk doesn't use a standard json array. This method ensures that the enumerable of events is 
        /// serialized property to be ingested by Splunk.
        /// </remarks>
        /// <param name="splunkEvents"></param>
        /// <returns>The deserialized resonse from Splunk.</returns>
        /// <exception cref="ArgumentNullException">If splunkEvents is null.</exception>
        /// <exception cref="JsonException">If the payload cannot be serialized into json.</exception>
        /// <exception cref="FormatException">If the payload is too large and can't be split into valid ingestable chuncks.</exception>
        public SplunkResponse SendToSplunk(IEnumerable<SplunkEventMetadata> splunkEvents)
        {
            if (splunkEvents == null)
                throw new ArgumentNullException(nameof(splunkEvents));

            if (splunkEvents.Count() == 0)
                return new SplunkResponse { Code = 5, Text = "No data" };

            //Microsoft's pattern for preventing deadlocks
            return Task.Run(() => SendToSplunkAsync(splunkEvents))
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        /// <summary>
        /// Sends the provided SplunkEventMetadata to Splunk.
        /// </summary>
        /// <remarks>
        /// Splunk doesn't use a standard json array. This method ensures that the enumerable of events is 
        /// serialized property to be ingested by Splunk.
        /// </remarks>
        /// <param name="splunkEvents"></param>
        /// <returns>The deserialized resonse from Splunk.</returns>
        /// <exception cref="ArgumentNullException">If splunkEvents is null.</exception>
        /// <exception cref="JsonException">If the payload cannot be serialized into json.</exception>
        /// <exception cref="FormatException">If the payload is too large and can't be split into valid ingestable chuncks.</exception>
        public async Task<SplunkResponse> SendToSplunkAsync(IEnumerable<SplunkEventMetadata> splunkEvents)
        {
            if (splunkEvents == null)
                throw new ArgumentNullException(nameof(splunkEvents));

            if (splunkEvents.Count() == 0)
                return new SplunkResponse { Code = 5, Text = "No data" };

            SplunkResponse splunkResponse = null;
            StringBuilder splunkPayload = new StringBuilder();

            foreach(var splunkEvent in splunkEvents)
            {
                splunkPayload.Append(JsonConvert.SerializeObject(splunkEvent));

                if (splunkPayload.Length > 5_000_000)
                    throw new FormatException("Payload too large. Splunk has a limit of 5MB and the events provided cannot be split into smaller chunks. ie. the first message or two is very large.");

                // Splunk has a payload limit of 5MB. Capping the payload at 2MB ensures that a large message doesn't
                // cause the payload to exceed that.

                if (splunkPayload.Length > 2_048_000)
                {
                    splunkResponse = await SendToSplunkAsync(splunkPayload.ToString());
                    splunkPayload.Clear();
                }
            }

            if (splunkPayload.Length > 0)
                splunkResponse = await SendToSplunkAsync(splunkPayload.ToString());
            
            return splunkResponse;
        }
        
        /// <summary>
        /// Sends the provided splunk formatted json payload to Splunk.
        /// </summary>
        /// <param name="requestJson">A valid Splunk json payload.</param>
        /// <returns>The deserialized resonse from Splunk.</returns>
        async Task<SplunkResponse> SendToSplunkAsync(string requestJson)
        {
            if (string.IsNullOrWhiteSpace(requestJson))
                throw new ArgumentException("Payload cannot be blank", nameof(requestJson));

            if (requestJson.Length > 5_000_000)
                throw new FormatException("Payload too large. Splunk has a limit of 5MB and the events provided cannot be split into smaller chunks.");

            // Check the circuit breaker to see if we should allow the request through.
            // If not, short-circuit and perform the fallback action.
            if (!ShouldAllowRequest())
            {
                RetryLater(requestJson);
                return HandleFallback(requestJson);
            }

            // Obtain a semephore for bulkheading. This reduces the likelyhood of storming Splunk.
            _bulkheadSemaphore.Wait();

            SplunkResponse splunkResponse = null;
            HttpResponseMessage response = null;
            string repsonseJson = null;
            bool failureWasCounted = false;
            Metrics.IncrementRequestCount();
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                StringContent content = new StringContent(requestJson);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                content.Headers.ContentEncoding.Add("gzip");

                response = await httpClient.PostAsync(options.Uri, content);

                
                if (response.Content != null)
                    repsonseJson = await response.Content?.ReadAsStringAsync();

                if (!string.IsNullOrWhiteSpace(repsonseJson))
                    splunkResponse = JsonConvert.DeserializeObject<SplunkResponse>(repsonseJson);

                if (!response.IsSuccessStatusCode)
                {
                    Metrics.IncrementFailureCount();
                    failureWasCounted = true;

                    Trace.TraceError($"{options.AppName}: EventId: {EventIds.DependencyServiceFailure} " +
                        $"EventCode: {nameof(EventIds.DependencyServiceFailure)} {nameof(SplunkHttpCollectorClient)}: " +
                        $"An error occured while writing to Splunk." +
                        $"Original message: {requestJson} " +
                        $"Reponse from Splunk: {repsonseJson}");
                }
                else
                {
                    Metrics.IncrementSuccessCount();

                    // Call to Splunk was successful. If we're half open, Close the circuit breaker.
                    if (_circuitBreakerState == CircuitBreakerStateConstants.HalfOpen)
                    {
                        CloseCircuitBreaker();
                    }
                }
            }
            catch (Exception ex)
            {
                RetryLater(requestJson);

                if (!failureWasCounted)
                {
                    Metrics.IncrementFailureCount();
                    failureWasCounted = true;
                }

                splunkResponse = new SplunkResponse
                {
                    Code = EventIds.DependencyServiceFailure,
                    Text = ex.ToString()
                };

                Trace.TraceError($"{options.AppName}: EventId: {EventIds.DependencyServiceFailure} " +
                                 $"EventCode: {nameof(EventIds.DependencyServiceFailure)} {nameof(SplunkHttpCollectorClient)}: " +
                                 $"An error occured while writing to Splunk: {ex} " +
                                 $"Original message: {requestJson} " +
                                 $"Reponse from Splunk: {repsonseJson}");
            }
            finally
            {
                _bulkheadSemaphore.Release();

                sw.Stop();
                Metrics.MeasureExectutionTime(sw.ElapsedMilliseconds);

                if (failureWasCounted && _circuitBreakerState != CircuitBreakerStateConstants.Open)
                {
                    // There was a failure and the breaker is closed or half open. 
                    // Check the code to see if the circuit breaker should be tripped.
                    if ( splunkResponse == null ||    // something really bad happened
                         splunkResponse.Code > 14 ||  // other status code not provided by splunk
                        (splunkResponse.Code >= 1 &&  // forbidden and unauthorized should trip the breaker
                         splunkResponse.Code <= 4) || // forbidden and unauthorized should trip the breaker 
                         splunkResponse.Code == 8 ||  // Internal Server Error
                         splunkResponse.Code == 9)    // Service Unavailable
                    {
                        OpenCircuitBreaker();
                    }
                }
            }
            
            return splunkResponse;
        }

        void OnTransientRetry(object sender, RetryingEventArgs e)
        {
            Trace.TraceWarning($"{options.AppName}: EventId: {EventIds.DependencyServiceFault} " +
                               $"EventCode: {nameof(EventIds.DependencyServiceFault)} {nameof(SplunkHttpCollectorClient)}: " +
                               $"A transient fault occured while communicating with Splunk." +
                               $"LastException: {e.LastException}: " +
                               $"Retries: {e.CurrentRetryCount}");

            Metrics.IncrementTransientFaultCount();

            Retrying?.Invoke(this, e);
        }

        void OpenCircuitBreaker()
        {
            Interlocked.Exchange(ref _circuitBreakerState, CircuitBreakerStateConstants.Open);
            Interlocked.Exchange(ref _circuitBreakerOpenTime, DateTime.UtcNow.Ticks);

            CircuitBreakerStateChanged?.BeginInvoke(this, SplunkHttpCollectorClient.CircuitBreakerState, null, null);
        }

        void CloseCircuitBreaker()
        {
            Interlocked.Exchange(ref _circuitBreakerOpenTime, -1);
            Interlocked.Exchange(ref _circuitBreakerState, CircuitBreakerStateConstants.Closed);

            CircuitBreakerStateChanged?.BeginInvoke(this, SplunkHttpCollectorClient.CircuitBreakerState, null, null);

            DrainRetryQueue().ContinueWith(t =>
            {
                Trace.TraceError(t.Exception.ToString());
            },TaskContinuationOptions.OnlyOnFaulted);
        }

        void HalfOpenCircuitBreaker()
        {
            Interlocked.Exchange(ref _circuitBreakerState, CircuitBreakerStateConstants.HalfOpen);

            CircuitBreakerStateChanged?.BeginInvoke(this, SplunkHttpCollectorClient.CircuitBreakerState, null, null);
        }

        bool ShouldAllowRequest()
        {
            if (Interlocked.Read(ref _circuitBreakerOpenTime) == -1)
                return true;

            long sinceTripped = DateTime.UtcNow.Ticks - Interlocked.Read(ref _circuitBreakerOpenTime);

            //If the circuit breaker is open and retry window has expires, then try a single request...
            if (_circuitBreakerState == CircuitBreakerStateConstants.Open && sinceTripped >= options.CircuitBreakerRetryWindow.Ticks)
            {
                HalfOpenCircuitBreaker();
                return true;
            }

            return false;
        }

        SplunkResponse HandleFallback(string splunkJson)
        {
            if (fallback != null)
            {
                try
                {
                    fallback(splunkJson);
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Fallback error: {0}", ex);
                }
            }
            else
            {
                Trace.TraceWarning("Splunk short-circuited, but no fallback was provided. Log writes might be lost.");
            }

            return new SplunkResponse
            {
                Code = EventIds.DependencyServiceFailure,
                Text = "Short-Circuited. Fallback was called instead."
            }; ;
        }

        void RetryLater(string message)
        {
            //We don't want to add to the memory pressure, so we'll only enqueue up to the buffer size
            if (retryQueue.Count < options.RetryQueueBufferSize)
                retryQueue.Enqueue(message);
        }

        async Task DrainRetryQueue()
        {
            while(!retryQueue.IsEmpty)
            {
                retryQueue.TryDequeue(out string message);
                await SendToSplunkAsync(message);
            }
        }
        #region IDisposable Support
        bool disposedValue = false; // To detect redundant calls

        void Dispose(bool disposing)
        {
            try
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        httpClient?.Dispose();
                    }

                    httpClient = null;
                    options = null;
                    Retrying = null;

                    disposedValue = true;
                }
            }
            catch
            {
                //Never blow up the finalizer.
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~SplunkHttpCollectorClient()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        /// <summary>
        /// Disposes of managed and unmanaged resources held by this object.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
