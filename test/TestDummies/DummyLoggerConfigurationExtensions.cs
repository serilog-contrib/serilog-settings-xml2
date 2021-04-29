using Serilog;
using Serilog.Configuration;
using Serilog.Events;
using Serilog.Formatting;
using System;
using System.Collections.Generic;

namespace TestDummies
{
    public static class DummyLoggerConfigurationExtensions
    {
        public static LoggerConfiguration DummyRollingFile(
            this LoggerSinkConfiguration loggerSinkConfiguration,
            string pathFormat,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            string outputTemplate = null,
            IFormatProvider formatProvider = null)
        {
            return loggerSinkConfiguration.Sink(new DummyRollingFileSink(), restrictedToMinimumLevel);
        }

        public static LoggerConfiguration DummyRollingFile(
            this LoggerSinkConfiguration loggerSinkConfiguration,
            ITextFormatter formatter,
            string pathFormat,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum)
        {
            return loggerSinkConfiguration.Sink(new DummyRollingFileSink(), restrictedToMinimumLevel);
        }

        public static LoggerConfiguration DummyRollingFile(
            this LoggerSinkConfiguration loggerSinkConfiguration,
            string[] stringArrayBinding,
            string pathFormat,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum)
        {
            return loggerSinkConfiguration.Sink(new DummyRollingFileSink(), restrictedToMinimumLevel);
        }

        public static LoggerConfiguration DummyRollingFile(
            this LoggerSinkConfiguration loggerSinkConfiguration,
            int[] intArrayBinding,
            string pathFormat,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum)
        {
            return loggerSinkConfiguration.Sink(new DummyRollingFileSink(), restrictedToMinimumLevel);
        }

        public static LoggerConfiguration DummyRollingFile(
            this LoggerAuditSinkConfiguration loggerSinkConfiguration,
            string pathFormat,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            string outputTemplate = null,
            IFormatProvider formatProvider = null)
        {
            return loggerSinkConfiguration.Sink(new DummyRollingFileAuditSink(), restrictedToMinimumLevel);
        }

        public static LoggerConfiguration DummyRollingFile(
            this LoggerSinkConfiguration loggerSinkConfiguration,
            List<Binding> objectBinding,
            string pathFormat,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum)
        {
            return loggerSinkConfiguration.Sink(new DummyRollingFileSink(), restrictedToMinimumLevel);
        }

        public class Binding
        {
            public string Foo { get; set; }

            public string Abc { get; set; }
        }

        public static LoggerConfiguration DummyArrayOfType(this LoggerDestructuringConfiguration loggerSinkConfiguration,
            List<Type> list,
            Type[] array = null,
            Type type = null,
            CustomCollection<Type> custom = null,
            CustomCollection<string> customString = null)
        {
            return loggerSinkConfiguration.With(DummyPolicy.Current = new DummyPolicy
            {
                List = list,
                Array = array,
                Type = type,
                Custom = custom,
                CustomStrings = customString,
            });
        }

    }
}
