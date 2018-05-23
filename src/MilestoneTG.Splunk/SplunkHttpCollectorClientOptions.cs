using System;

namespace MilestoneTG.Splunk
{
    /// <summary>
    /// Configuration Options for the <see cref="SplunkHttpCollectorClient"/>.
    /// </summary>
    /// <example>
    /// An appSettings.json entry using these options would look like this:
    /// <code>
    /// "Logging": {
    ///  "Splunk": {
    ///    "Url": "https://http-inputs-MilestoneTG-inc.splunkcloud.com/services/collector/event",
    ///    "ApiKey": "603E0E45-C9D6-58FC-B896-0489891D7C66",
    ///    "AppName": "MyCoolService",
    ///    "FlushInterval": "00:01:00",
    ///    "BufferSize": 100,
    ///    "LogLevel": {
    ///      "Default": "Information"
    ///      "System": "Error",
    ///      "Microsoft": "Error"
    ///     }
    ///   }
    /// }
    /// </code>
    /// </example>
    public class SplunkHttpCollectorClientOptions
    {
        /// <summary>
        /// The URI for your Splunk endpoint.
        /// </summary>
        /// <value>A valid URI</value>
        public string Uri { get; set; }

        /// <summary>
        /// The Splunk API key.
        /// </summary>
        /// <value>
        /// A valid api key. See the wiki for more information: http://core.aws.corppvt.cloud/corewiki/index.php?title=Application_Logging 
        /// </value>
        public string ApiKey { get; set; }

        /// <summary>
        /// The application name. This value is used for the <c>"name"</c> attribute in the log entry.
        /// </summary>
        public string AppName { get; set; }

        /// <summary>
        /// The amount of time to wait once the circuitbreaker is tripped before trying again.
        /// The default value is 10 minutes.
        /// </summary>
        /// <value>The default value is 10 minutes.</value>
        public TimeSpan CircuitBreakerRetryWindow { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// The timespan to wait before the request times out. The default value is 100,000 milliseconds (100 seconds).
        /// </summary>
        /// <value>
        /// The default value is 100,000 milliseconds (100 seconds).
        /// </value>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);

        /// <summary>
        /// If Splunk is unavailable (circuit breaker open), the number of log entries to buffer 
        /// in the retry queue to write when Splunk is available again. The default is 100.
        /// </summary>
        /// <value>The default is 100.</value>
        public int RetryQueueBufferSize { get; set; } = 100;
    }
}