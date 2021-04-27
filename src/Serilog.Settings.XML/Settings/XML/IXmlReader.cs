using Serilog.Configuration;

namespace Serilog.Settings.XML
{
    interface IXmlReader : ILoggerSettings
    {
        void ApplySinks(LoggerSinkConfiguration loggerSinkConfiguration);
        void ApplyEnrichment(LoggerEnrichmentConfiguration loggerEnrichmentConfiguration);
    }
}
