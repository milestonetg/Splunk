using System;
using System.Collections.Generic;
using System.Text;

namespace MilestoneTG.Splunk.FailureInjectionTesting
{
    /// <summary>
    /// Type of failure to inject
    /// </summary>
    public enum FailureType
    {
        /// <summary>
        /// Will inject the configured HTTP status code as the result.
        /// </summary>
        HttpStatusCode,

        /// <summary>
        /// Will inject latency into all calls.
        /// </summary>
        Latency,

        /// <summary>
        /// Will randomly select a failure response or success based on the current
        /// millisecond value.
        /// </summary>
        Random
    }
}
