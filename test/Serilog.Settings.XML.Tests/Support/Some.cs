using Serilog.Events;
using Serilog.Parsing;
using System;
using System.Linq;
using System.Threading;

namespace Serilog.Settings.XML.Tests.Support
{
    static class Some
    {
        static int Counter;

        public static int Int() =>
            Interlocked.Increment(ref Counter);

        public static string String(string tag = null) =>
            $"{tag ?? ""}__{Int()}";

        public static TimeSpan TimeSpan() =>
            System.TimeSpan.FromMinutes(Int());

        public static DateTime Instant() =>
            new DateTime(2012, 10, 28) + TimeSpan();

        public static DateTimeOffset OffsetInstant() =>
            new(Instant());

        public static MessageTemplate MessageTemplate() =>
            new MessageTemplateParser().Parse(String());

        public static LogEvent LogEvent(LogEventLevel level) =>
            new(Instant(), level, null, MessageTemplate(), Enumerable.Empty<LogEventProperty>());

        public static LogEvent DebugEvent() =>
            LogEvent(LogEventLevel.Debug);

        public static LogEvent InformationEvent() =>
            LogEvent(LogEventLevel.Information);

        public static LogEvent WarningEvent() =>
            LogEvent(LogEventLevel.Warning);

        public static LogEvent ErrorEvent() =>
            LogEvent(LogEventLevel.Error);

        public static LogEvent FatalEvent() =>
            LogEvent(LogEventLevel.Fatal);

    }
}
