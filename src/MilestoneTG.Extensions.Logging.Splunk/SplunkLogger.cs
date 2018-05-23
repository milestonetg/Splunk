using MilestoneTG.Splunk;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Internal;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MilestoneTG.Extensions.Logging.Splunk
{
    /// <summary>
    /// A <see cref="Microsoft.Extensions.Logging.ILogger"/> implementation for Splunk.
    /// </summary>
    /// <remarks>
    /// Log entries are buffered to an in-memory queue to reduce the performance hit
    /// that can be caused by logging. The buffer size and flush timer interval to trigger
    /// the actual write to Splunk can be configured using the <see cref="SplunkLoggerOptions"/>.
    /// Error and Critical log levels cause the buffer to flush immediately.
    /// 
    /// > [!IMPORTANT]
    /// > Because log entries are buffered, a process crash can result in lost entries.
    /// 
    /// > [!TIP]
    /// > You can disable buffering by setting the buffer size to 0.
    /// 
    /// </remarks>
    public class SplunkLogger : ILogger, IDisposable
    {
        SplunkLoggerOptions options;
        string name;
        ConcurrentQueue<SplunkEventMetadata> buffer = new ConcurrentQueue<SplunkEventMetadata>();
        Timer timer;

        SplunkHttpCollectorClient splunkClient;

        /// <summary>
        /// Gets a singleton instance of the named Logger
        /// </summary>
        /// <param name="name">Logger name.</param>
        /// <param name="options">Logger configuration options</param>
        /// <returns></returns>
        public static SplunkLogger GetLogger(string name, SplunkLoggerOptions options)
        {
            return _loggerInstances.GetOrAdd(name, new SplunkLogger(name, options));
        }
        static ConcurrentDictionary<string,SplunkLogger> _loggerInstances = new ConcurrentDictionary<string, SplunkLogger>();

        /// <summary>
        /// Inializes an new instance.
        /// </summary>
        /// <param name="name">Logger name. Corresponds to the category name and will translate 
        /// to the <see cref="LogEntry"/>.ComponentName property.</param>
        /// <param name="options">Configuration options.</param>
        private SplunkLogger(string name, SplunkLoggerOptions options)
        {
            this.name = name;
            this.options = options;

            this.splunkClient = SplunkHttpCollectorClient.GetInstance(options);

            timer = new Timer((o) => Flush(), null, options.FlushInterval, options.FlushInterval);
        }

        /// <summary>
        /// Gets a readonly copy of the current running log levels.
        /// </summary>
        /// <returns></returns>
        public IReadOnlyCollection<KeyValuePair<string, LogLevel>> GetRunningLoggingLevels()
        {
            KeyValuePair<string, LogLevel>[] copyOfLoggerLevels = new KeyValuePair<string, LogLevel>[options.LogLevel.Count];

            lock (options)
            {
                options.LogLevel.CopyTo(copyOfLoggerLevels, 0);
            }
            return copyOfLoggerLevels;
        }

        /// <summary>
        /// Sets the logging level of a the given logger name to the specificed level.
        /// </summary>
        /// <param name="loggerName">The name of the logger for which to adjust the level.</param>
        /// <param name="newLevel">The new <see cref="LogLevel"/> value.</param>
        public void SetLogLevel(string loggerName, LogLevel newLevel)
        {
            lock (options)
            {
                options.LogLevel[loggerName] = newLevel;
            }
        }

        /// <summary>
        /// Sets the logging level for the logger names provided.
        /// </summary>
        /// <param name="newLoggerLevels">A dictionary of logger names and their desired level.</param>
        public void SetLogLevel(IDictionary<string, LogLevel> newLoggerLevels)
        {
            foreach (string loggerName in newLoggerLevels.Keys)
                SetLogLevel(loggerName, newLoggerLevels[loggerName]);
        }

        /// <summary>
        /// Begins a logical operation scope.
        /// </summary>
        /// <typeparam name="TState"></typeparam>
        /// <param name="state">The identifier for the scope.</param>
        /// <returns>An IDisposable that ends the logical operation scope on dispose.</returns>
        /// <remarks>SplunkLogger does not support scoped logging.</remarks>
        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        /// <summary>
        /// Checks if the given logLevel is enabled.
        /// </summary>
        /// <param name="logLevel">level to be checked.</param>
        /// <returns>true if enabled.</returns>
        public bool IsEnabled(LogLevel logLevel)
        {
            LogLevel enabledLevel;

            string ruleKey = options.LogLevel.Keys.FirstOrDefault(k => name.StartsWith(k));
            if (ruleKey != null)
                enabledLevel = options.LogLevel[ruleKey];
            else if (options.LogLevel.ContainsKey("Default"))
                enabledLevel = options.LogLevel["Default"];
            else
                enabledLevel = LogLevel.Information;

            return logLevel >= enabledLevel && enabledLevel != LogLevel.None;
        }

        /// <summary>
        /// Writes a log entry.
        /// </summary>
        /// <typeparam name="TState">Type of the state parameter.</typeparam>
        /// <param name="logLevel">Entry will be written on this level.</param>
        /// <param name="eventId">Id of the event.</param>
        /// <param name="state">The entry to be written. Can be also an object.</param>
        /// <param name="exception">The exception related to this entry.</param>
        /// <param name="formatter">Function to create a string message of the state and exception.</param>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            Dictionary<string, object> splunkEvent = new Dictionary<string, object>
            {
                { "applicationName", options.AppName },
                { "loggerName", name },
                { "severityLevel", logLevel.ToString() },
                { "message", formatter(state, exception) }
            };

            SplunkEventMetadata splunkEventMetadata = new SplunkEventMetadata
            {
                Source = options.AppName,
                Event = splunkEvent
            };
            
            FixData(splunkEvent, state);
            FixException(splunkEvent, exception);

            if (options.BufferSize == 0)
            {
                splunkClient.SendToSplunk(splunkEventMetadata);
            }
            else
            {
                buffer.Enqueue(splunkEventMetadata);

                if (buffer.Count >= options.BufferSize || (logLevel >= LogLevel.Error && options.FlushOnError))
                    Task.Factory.StartNew(() => Flush());
            }
        }

        /// <summary>
        /// Transfers the values from state to the <see cref="LogEntry"/>.Data dictionary.
        /// </summary>
        /// <param name="splunkEvent"></param>
        /// <param name="state"></param>
        void FixData(Dictionary<string, object> splunkEvent, object state)
        {
            if (state is FormattedLogValues formattedLogValues)
            {
                foreach (var kv in formattedLogValues)
                {
                    if (kv.Key == "{OriginalFormat}")
                        continue;

                    splunkEvent[kv.Key] = kv.Value;
                }
            }
            else if (state is Dictionary<string, object> data)
            {
                if (data.ContainsKey("FormattedLogValues"))
                {
                    var flv = (FormattedLogValues)data["FormattedLogValues"];
                    foreach (var kv in flv)
                    {
                        if (kv.Key == "{OriginalFormat}")
                            continue;

                        splunkEvent[kv.Key] = kv.Value;
                    }

                    data.Remove("FormattedLogValues");
                }

                foreach(var kvp in data)
                    splunkEvent[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        /// Adds the exception to the <see cref="LogEntry"/>
        /// </summary>
        /// <param name="splunkEvent"></param>
        /// <param name="exception"></param>
        void FixException(Dictionary<string, object> splunkEvent, Exception exception)
        {
            if (exception != null)
            {
                splunkEvent["exception"] = exception.Message;
                splunkEvent["exceptionDetail"] = exception.ToString();
            }
        }

        void Flush()
        {
            try
            {
                SplunkEventMetadata splunkEventMetadata = null;
                List<SplunkEventMetadata> splunkEvents = new List<SplunkEventMetadata>();
                do
                {
                    int counter = 0;
                    while (buffer.TryDequeue(out splunkEventMetadata) && counter < 10)
                    {
                        splunkEvents.Add(splunkEventMetadata);
                        counter++;
                    }

                    if (splunkEvents.Count > 0)
                    {
                        splunkClient.SendToSplunk(splunkEvents);
                        splunkEvents.Clear();
                    }

                } while (!buffer.IsEmpty);
            }
            catch
            {
                //SplunkHttpCollectorClient should never throw and exception, but just in case
                //there is an edge case in the routine we haven't accounted for,
                //don't crash the background thread if something happens.
            }
            
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        /// <summary>
        /// Disposes of the instance.
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (timer != null)
                        timer.Dispose();

                    if (buffer.Count > 0)
                        Flush();
                }
                timer = null;
                buffer = null;
                
                disposedValue = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~SplunkLogger()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        /// <summary>
        /// Disposes of this instance.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}