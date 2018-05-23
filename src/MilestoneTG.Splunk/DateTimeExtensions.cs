using System;
using System.Collections.Generic;
using System.Text;

namespace MilestoneTG.Splunk
{
    /// <summary>
    /// DateTime/DateTimeOffset extensions
    /// </summary>
    public static class DateTimeExtensions
    {
        static long _minEpoch = new DateTime(1970,1,1).Ticks / TimeSpan.TicksPerSecond;

        /// <summary>
        /// Converts the DateTime instance to Unix Epoch Time maintaining millisecond precision.
        /// </summary>
        /// <param name="dt"></param>
        /// <returns>The number of seconds since Jan 1, 1970</returns>
        public static double ToEpoch(this DateTime dt)
        {
            double instanceSeconds = dt.Ticks / TimeSpan.TicksPerSecond;
            return (instanceSeconds - _minEpoch) + (dt.Millisecond / 1000.0);
        }

        /// <summary>
        /// Converts the DateTimeOffset instance to Unix Epoch Time maintaining millisecond precision.
        /// </summary>
        /// <param name="dt"></param>
        /// <returns>The number of seconds since Jan 1, 1970</returns>
        /// <remarks>
        /// In net46 and higher, there is a method, ToUnixTimeSeconds() which returns epoch in whole seconds. If you 
        /// need millisecond precision, since Splunk supports it, use the ToEpoch() extension method instead.
        /// </remarks>
        public static double ToEpoch(this DateTimeOffset dt)
        {
            double instanceSeconds = dt.Ticks / TimeSpan.TicksPerSecond;
            return (instanceSeconds - _minEpoch) + (dt.Millisecond / 1000.0);
        }
    }
}
