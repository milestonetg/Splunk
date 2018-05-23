using MilestoneTG.Splunk;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace MilestoneTG.Extensions.Logging.Splunk
{
    /// <summary>
    /// Configuration Options for the <see cref="SplunkLogger"/>.
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
    public class SplunkLoggerOptions : SplunkHttpCollectorClientOptions
    {
        /// <summary>
        /// A dictionary of LogLevel switches.
        /// </summary>
        /// <value>
        /// A dictionary whose key is the category name/prefix and value is a valid <see cref="Microsoft.Extensions.Logging.LogLevel"/>.
        /// </value>
        public IDictionary<string, LogLevel> LogLevel { get; set; } = new Dictionary<string, LogLevel>();

        /// <summary>
        /// The timer interval to trigger a flush of the log entries to Splunk.
        /// This will trigger a flush regarless of the current size of the buffer.
        /// </summary>
        /// <value>Default is 30 seconds.</value>
        public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// The log entry count to trigger a flush of the log entries to Splunk.
        /// This will trigger a flush regardless of the FlushInterval. To disable
        /// buffering, set this value to 0.
        /// </summary>
        /// <value>Default is 100.</value>
        public int BufferSize { get; set; } = 100;

        /// <summary>
        /// Whether or not to flush the buffer immediately when an error or critical entry is logged.
        /// </summary>
        /// <value>Default is false.</value>
        public bool FlushOnError { get; set; }
    }
}