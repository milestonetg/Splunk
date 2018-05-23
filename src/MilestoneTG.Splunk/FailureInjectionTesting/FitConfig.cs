namespace MilestoneTG.Splunk.FailureInjectionTesting
{
    /// <summary>
    /// Chaos Testing configuration utility
    /// </summary>
    public static class ChaosConfig
    {
        /// <summary>
        /// Gets a value indicating whether Chaos testing is enabled.
        /// </summary>
        /// <value>
        ///   <c>true</c> if enabled; otherwise, <c>false</c>.
        /// </value>
        public static bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the FailureType.
        /// </summary>
        public static FailureType FailureType { get; set; }

        /// <summary>
        /// Gets or sets the HTTP status code to use if the FailureType is
        /// <see cref="FailureType.HttpStatusCode"/> or the milliseconds of latency
        /// to inject if the FailureType is <see cref="FailureType.Latency"/>. It is ignored
        /// for FailureType <see cref="FailureType.Random"/>.
        /// </summary>
        public static int Value { get; set; }
    }
}