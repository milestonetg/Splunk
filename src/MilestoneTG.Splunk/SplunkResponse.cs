using Newtonsoft.Json;

namespace MilestoneTG.Splunk
{
    /// <summary>
    /// Encapsulates a response from the Splunk services/collector endpoint.
    /// http://docs.splunk.com/Documentation/SplunkCloud/6.6.3/RESTREF/RESTinput#services.2Fcollector
    /// </summary>
    public class SplunkResponse
    {
        /// <summary>
        /// Human readable status, same value as code. Ex: "Success"
        /// </summary>
        [JsonProperty(PropertyName = "text")]
        public string Text { get; set; }

        /// <summary>
        /// Machine format status, same value as text. Ex: 0
        /// </summary>
        [JsonProperty(PropertyName = "code")]
        public int Code { get; set; }

        /// <summary>
        /// When errors occur, indicates the zero-based index of first invalid event in an event sequence.
        /// </summary>
        [JsonProperty(PropertyName = "invalid-event-number")]
        public int InvalidEventNumber { get; set; }

        /// <summary>
        /// If useACK is enabled for the token, indicates the ackId to use for checking an indexer acknowledgement.
        /// </summary>
        [JsonProperty(PropertyName = "ackId")]
        public string AckId { get; set; }

        /// <summary>
        /// Returns the JSON representation of this instance as a string.
        /// </summary>
        /// <returns>JSON</returns>
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
