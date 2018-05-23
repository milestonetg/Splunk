using Newtonsoft.Json;
using System.Net;
using System.Net.Http;

namespace MilestoneTG.Splunk.FailureInjectionTesting
{
    /// <summary>
    /// Known Splunk repsonse messages
    /// http://docs.splunk.com/Documentation/SplunkCloud/6.6.3/RESTREF/RESTinput#services.2Fcollector
    /// </summary>
    public static class KnownResponses
    {
        /*
            0	200	OK	Success
            1	403	Forbidden	Token disabled
            2	401	Unauthorized	Token is required
            3	401	Unauthorized	Invalid authorization
            4	403	Forbidden	Invalid token
            5	400	Bad Request	No data
            6	400	Bad Request	Invalid data format
            7	400	Bad Request	Incorrect index
            8	500	Internal Error	Internal server error
            9	503	Service Unavailable	Server is busy
            10	400	Bad Request	Data channel is missing
            11	400	Bad Request	Invalid data channel
            12	400	Bad Request	Event field is required
            13	400	Bad Request	Event field cannot be blank
            14	400	Bad Request	ACK is disabled
         */

        /// <summary>
        /// <see cref="SplunkResponse"/> messages.
        /// </summary>
        public static SplunkResponse[] SplunkRepsonses { get; }

        /// <summary>
        /// <see cref="HttpResponseMessage"/>s
        /// </summary>
        public static HttpStatusCode[] HttpStatusCodes { get; }

        static KnownResponses()
        {
            SplunkRepsonses = new[] {
                new SplunkResponse { Code = 0, Text = "Success" },
                new SplunkResponse { Code = 1, Text = "Token disabled" },
                new SplunkResponse { Code = 2, Text = "Token is required" },
                new SplunkResponse { Code = 3, Text = "Invalid authorization" },
                new SplunkResponse { Code = 4, Text = "Invalid token" },
                new SplunkResponse { Code = 5, Text = "No data" },
                new SplunkResponse { Code = 6, Text = "Invalid data format" },
                new SplunkResponse { Code = 7, Text = "Incorrect index" },
                new SplunkResponse { Code = 8, Text = "Internal server error" },
                new SplunkResponse { Code = 9, Text = "Server is busy" },
                new SplunkResponse { Code = 10, Text = "Data channel is missing" },
                new SplunkResponse { Code = 11, Text = "Invalid data channel" },
                new SplunkResponse { Code = 12, Text = "Event field is required" },
                new SplunkResponse { Code = 13, Text = "Event field cannot be blank" },
                new SplunkResponse { Code = 14, Text = "ACK is disabled" },
            };

            HttpStatusCodes = new[] {
                HttpStatusCode.OK,
                HttpStatusCode.Forbidden,
                HttpStatusCode.Unauthorized,
                HttpStatusCode.Unauthorized,
                HttpStatusCode.Forbidden,
                HttpStatusCode.BadRequest,
                HttpStatusCode.BadRequest,
                HttpStatusCode.BadRequest,
                HttpStatusCode.InternalServerError,
                HttpStatusCode.ServiceUnavailable,
                HttpStatusCode.BadRequest,
                HttpStatusCode.BadRequest,
                HttpStatusCode.BadRequest,
                HttpStatusCode.BadRequest,
                HttpStatusCode.BadRequest,
            };
        }

        /// <summary>
        /// Gets a <see cref="SplunkResponse"/> based on the Splunk return code.
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public static SplunkResponse GetSplunkResponseForCode(int code)
        {
            if (code >= 0 && code < SplunkRepsonses.Length)
                return SplunkRepsonses[code];

            return null;
        }

        /// <summary>
        /// Gets an <see cref="HttpResponseMessage"/> based on the Splunk return code.
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public static HttpResponseMessage GetHttpResponseMessageForCode(int code)
        {
            if (code >= 0 && code < HttpStatusCodes.Length)
                return new HttpResponseMessage(HttpStatusCodes[code]) { Content = new StringContent(JsonConvert.SerializeObject(SplunkRepsonses[code])) };
            
            return null;
        }
    }
}
