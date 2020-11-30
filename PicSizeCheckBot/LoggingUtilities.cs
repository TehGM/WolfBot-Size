using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using TehGM.Wolfringo.Messages;

namespace TehGM.Wolfringo.Utilities
{
    /// <summary>Utilities for logging the exception with scope preserved using 'when' keyword.</summary>
    public static class LoggingUtilities
    {
        public static IDisposable BeginLogScope(this ChatMessage message, ILogger log)
        {
            return log.BeginScope(new Dictionary<string, object>()
                {
                    { "MessageText", message.Text },
                    { "SenderID", message.SenderID.Value },
                    { "RecipientID", message.RecipientID },
                    { "GroupName", message.IsGroupMessage ? message.RecipientID.ToString() : null }
                });
        }
    }
}
