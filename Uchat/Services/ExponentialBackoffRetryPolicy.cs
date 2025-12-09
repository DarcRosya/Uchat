using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;

namespace Uchat.Services
{
    /// <summary>
    /// Custom retry policy with exponential backoff for SignalR reconnection
    /// </summary>
    public class ExponentialBackoffRetryPolicy : IRetryPolicy
    {
        private readonly TimeSpan[] _retryDelays = new[]
        {
            TimeSpan.FromSeconds(0),      // Immediate
            TimeSpan.FromSeconds(2),      // 2 seconds
            TimeSpan.FromSeconds(5),      // 5 seconds
            TimeSpan.FromSeconds(10),     // 10 seconds
            TimeSpan.FromSeconds(20),     // 20 seconds
            TimeSpan.FromSeconds(40),     // 40 seconds
            TimeSpan.FromSeconds(60),     // 1 minute
            TimeSpan.FromSeconds(120),    // 2 minutes
            TimeSpan.FromSeconds(300),    // 5 minutes
            TimeSpan.FromSeconds(600),    // 10 minutes
        };

        public TimeSpan? NextRetryDelay(RetryContext context)
        {
            if (context.PreviousRetryCount >= _retryDelays.Length)
            {
                // Stop retrying after max attempts
                return null;
            }

            return _retryDelays[context.PreviousRetryCount];
        }
    }
}