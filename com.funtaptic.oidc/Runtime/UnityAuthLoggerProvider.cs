using System;
using Microsoft.Extensions.Logging;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Funtaptic.OIDC
{
    public class UnityAuthLogger : ILogger
    {
        public string Category = "";

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            Debug.Log(string.Format("UnityAuthLogger: {0}, {1}, {2}", logLevel, exception, formatter(state, exception)));
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }
    }
    
    public class UnityAuthLoggerProvider : ILoggerProvider
    {
        public static UnityAuthLoggerProvider Instance = new UnityAuthLoggerProvider();
        
        public void Dispose()
        {
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new UnityAuthLogger();
        }
    }
}