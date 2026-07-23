using System;

namespace GetStream
{
    /// <summary>
    /// Opt-in auto-retry policy. Disabled by default: the client performs exactly one attempt and surfaces errors unchanged.
    /// When enabled, only GET/HEAD requests failing with HTTP 429 or a transport error are retried, and never when the backend marked the error unrecoverable.
    /// </summary>
    public class RetryConfig
    {
        /// <summary>Master switch. Default false.</summary>
        public bool Enabled { get; set; }

        /// <summary>Total attempt budget including the initial request. Default 3 (1 initial + 2 retries).</summary>
        public int MaxAttempts { get; set; } = 3;

        /// <summary>Cap applied to every wait between attempts, including Retry-After hints. Default 30s.</summary>
        public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromSeconds(30);
    }
}
