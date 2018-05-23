using System;
using Newtonsoft.Json;

namespace MilestoneTG.Splunk
{
    /// <summary>
    /// Encapsulates a Splunk Event Metadata.
    /// http://dev.splunk.com/view/event-collector/SP-CAAAE6P
    /// </summary>
    /// <example>
    /// Event metadata
    /// <code language="javascript">
    /// {
    ///    "time": 1426279439,
    ///    "host": "localhost",
    ///    "source": "datasource",
    ///    "sourcetype": "txt",
    ///    "index": "main",
    ///     "event": { 
    ///         "message": "Something happened",
    ///         "severity": "INFO"
    ///     }
    /// }  
    /// </code>
    /// </example>
public class SplunkEventMetadata
    {
        /// <summary>
        /// The event time. 
        /// The default time format is epoch time format, in the format sec.ms. 
        /// </summary>
        /// <value>
        /// Defaults to the current UTC time.
        /// </value>
        [JsonProperty(PropertyName = "time")]
        public double Time { get; set; } = DateTimeOffset.UtcNow.ToEpoch();

        /// <summary>
        /// The host value to assign to the event data. 
        /// This is typically the hostname of the client from which you're sending data.
        /// </summary>
        [JsonProperty(PropertyName = "host")]
        public string Host { get; set; }

        /// <summary>
        /// The source value to assign to the event data. 
        /// For example, if you're sending data from an app you're developing, 
        /// you could set this key to the name of the app.
        /// </summary>
        [JsonProperty(PropertyName = "source", NullValueHandling = NullValueHandling.Ignore)]
        public string Source { get; set; }

        /// <summary>
        /// The sourcetype value to assign to the event data.
        /// </summary>
        [JsonProperty(PropertyName = "sourcetype", NullValueHandling = NullValueHandling.Ignore)]
        public string SourceType { get; set; }

        /// <summary>
        /// The name of the index by which the event data is to be indexed. 
        /// The index you specify here must within the list of allowed indexes 
        /// if the token has the indexes parameter set.
        /// </summary>
        [JsonProperty(PropertyName = "index", NullValueHandling = NullValueHandling.Ignore)]
        public string Index { get; set; }

        /// <summary>
        /// Event data can be any serializable object or raw text.
        /// It's preferred that you use a seriazable POCO or Dictionary.
        /// </summary>
        /// <remarks>
        /// Encapsulates a Splunk Event.
        /// http://dev.splunk.com/view/event-collector/SP-CAAAE6P
        /// </remarks>
        /// <example>
        /// Event metadata
        /// <code language="cs">
        /// SplunkEventMetadata metaData = new SplunkEventMetadata();
        /// metaData.Event = new Dictionary()
        /// {
        ///     {"severity","Information"},
        ///     {"message","Test message"},
        ///     {"correlationId","1234567"}
        /// };
        /// </code>
        /// </example>
        [JsonProperty(PropertyName = "event")]
        public object Event { get; set; }

        /// <summary>
        /// Returns the JSON representation of this instance.
        /// </summary>
        /// <returns>JSON</returns>
        public override string ToString()
        {
#if DEBUG
            return JsonConvert.SerializeObject(this, Formatting.Indented);
#else
            return JsonConvert.SerializeObject(this, Formatting.None);
#endif
        }
    }
}