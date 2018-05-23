namespace MilestoneTG.Splunk
{
    /// <summary>
    /// Interlock operations cannot use enums, only constants or literals.
    /// These constants are used internally by the circuit breaker logic itself as well as to define the values
    /// of the <see cref="CircuitBreakerState"/> enum. This ensures consistency and readability.
    /// </summary>
    internal static class CircuitBreakerStateConstants
    {
        internal const int Closed = 0;
        internal const int Open = 1;
        internal const int HalfOpen = 2;
    }

    /// <summary>
    /// Supported CircuitBreaker states
    /// </summary>
    public enum CircuitBreakerState
    {
        /// <summary>
        /// Requests can pass through.
        /// </summary>
        Closed = CircuitBreakerStateConstants.Closed,

        /// <summary>
        /// Requests are short-circuited and the fallback is called instead.
        /// </summary>
        Open = CircuitBreakerStateConstants.Open,

        /// <summary>
        /// A single request is being allowed through to test service availability.
        /// </summary>
        HalfOpen = CircuitBreakerStateConstants.HalfOpen
    }
}
