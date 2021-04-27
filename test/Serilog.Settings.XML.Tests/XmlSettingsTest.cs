using Serilog.Core;
using Serilog.Enrichers;
using Serilog.Events;
using Serilog.Settings.XML.Tests.Support;
using System.Xml.Linq;
using Xunit;

namespace Serilog.Settings.XML.Tests
{
    public class XmlSettingsTest
    {
        private static LoggerConfiguration ConfigureXml(string xml)
        {
            XElement xElement = XElement.Parse(xml);
            return new LoggerConfiguration()
                .ReadFrom.Xml(xElement);
        }

        #region Enrichers

        [Fact]
        public void Enricher()
        {
            const string xml = @"<?xml version=""1.0"" standalone=""yes"" ?>
<Serilog>
    <Using>Serilog.Enrichers.Thread</Using>
    <Enricher Name=""WithThreadId"" />
</Serilog>";

            LogEvent evt = null;
            using Logger log = ConfigureXml(xml)
                .WriteTo.Sink(new DelegatingSink(e => evt = e))
                .CreateLogger();

            log.Write(Some.ErrorEvent());

            Assert.True(evt.Properties.ContainsKey(ThreadIdEnricher.ThreadIdPropertyName), "Enricher ThreadId was configured. It should be enriched.");
        }

        [Fact]
        public void EnricherWithSimpleArgs()
        {
            const string xml = @"<?xml version=""1.0"" standalone=""yes"" ?>
<Serilog>
    <Enricher Name=""WithProperty"">
        <Name>MyProperty</Name>
        <Value>123</Value>
    </Enricher>
</Serilog>";

            LogEvent evt = null;
            using Logger log = ConfigureXml(xml)
                .WriteTo.Sink(new DelegatingSink(e => evt = e))
                .CreateLogger();

            log.Write(Some.ErrorEvent());

            Assert.True(evt.Properties.ContainsKey("MyProperty"), "Enricher WithProperty was configured. It should be enriched.");
        }

        [Fact]
        public void EnricherWithStaticArgs()
        {
            const string xml = @"<?xml version=""1.0"" standalone=""yes"" ?>
<Serilog>
    <Enricher Name=""WithProperty"">
        <Name>Serilog.Enrichers.ThreadNameEnricher::ThreadNamePropertyName, Serilog.Enrichers.Thread</Name>
        <Value>DefaultThread</Value>
    </Enricher>
</Serilog>";

            LogEvent evt = null;
            using Logger log = ConfigureXml(xml)
                .WriteTo.Sink(new DelegatingSink(e => evt = e))
                .CreateLogger();

            log.Write(Some.ErrorEvent());

            Assert.True(evt.Properties.ContainsKey(ThreadNameEnricher.ThreadNamePropertyName), "Enricher WithProperty was configured. It should be enriched.");
        }

        [Fact]
        public void EnrichProperty()
        {
            const string xml = @"<?xml version=""1.0"" standalone=""yes"" ?>
<Serilog>
    <Property Name=""MyProperty"" Value=""Value"" />
</Serilog>";

            LogEvent evt = null;
            using Logger log = ConfigureXml(xml)
                .WriteTo.Sink(new DelegatingSink(e => evt = e))
                .CreateLogger();

            log.Write(Some.ErrorEvent());

            Assert.True(evt.Properties.ContainsKey("MyProperty"), "Property was configured. It should be enriched.");
        }

        [Fact]
        public void EnrichPropertyValue()
        {
            const string xml = @"<?xml version=""1.0"" standalone=""yes"" ?>
<Serilog>
    <Property Name=""MyProperty"">Value</Property>
</Serilog>";

            LogEvent evt = null;
            using Logger log = ConfigureXml(xml)
                .WriteTo.Sink(new DelegatingSink(e => evt = e))
                .CreateLogger();

            log.Write(Some.ErrorEvent());

            Assert.True(evt.Properties.ContainsKey("MyProperty"), "Property was configured. It should be enriched.");
        }

        #endregion

        #region MinimumLevel

        [Fact]
        public void MinimumLevel()
        {
            const string xml = @"<?xml version=""1.0"" standalone=""yes"" ?>
<Serilog>
    <MinimumLevel>Warning</MinimumLevel>
</Serilog>";

            LogEvent evt = null;
            using Logger log = ConfigureXml(xml)
                .WriteTo.Sink(new DelegatingSink(e => evt = e))
                .CreateLogger();

            log.Write(Some.InformationEvent());
            Assert.Null(evt);

            log.Write(Some.WarningEvent());
            Assert.NotNull(evt);
        }

        [Fact]
        public void DefaultMinimumLevel()
        {
            const string xml = @"<?xml version=""1.0"" standalone=""yes"" ?>
<Serilog>
    <MinimumLevel Default=""Warning"" />
</Serilog>";

            LogEvent evt = null;
            using Logger log = ConfigureXml(xml)
                .WriteTo.Sink(new DelegatingSink(e => evt = e))
                .CreateLogger();

            log.Write(Some.InformationEvent());
            Assert.Null(evt);

            log.Write(Some.WarningEvent());
            Assert.NotNull(evt);
        }

        [Theory]
        [InlineData("$switch1")]
        [InlineData("switch1")]
        public void MinimumLevelSwitch(string switchName)
        {
            string xml = @$"<?xml version=""1.0"" standalone=""yes"" ?>
<Serilog>
    <LevelSwitches>
        <Switch Name=""{switchName}"" Level=""Warning"" />
    </LevelSwitches>
    <MinimumLevel ControlledBy=""{switchName}"" />
</Serilog>";

            LogEvent evt = null;
            using Logger log = ConfigureXml(xml)
                .WriteTo.Sink(new DelegatingSink(e => evt = e))
                .CreateLogger();

            log.Write(Some.DebugEvent());
            Assert.True(evt is null, "LoggingLevelSwitch initial level was Warning. It should not log Debug messages");
            log.Write(Some.InformationEvent());
            Assert.True(evt is null, "LoggingLevelSwitch initial level was Warning. It should not log Information messages");
            log.Write(Some.WarningEvent());
            Assert.True(evt != null, "LoggingLevelSwitch initial level was Warning. It should log Warning messages");
        }

        [Fact]
        public void MinimumLevelOverride()
        {
            const string xml = @"<?xml version=""1.0"" standalone=""yes"" ?>
<Serilog>
    <MinimumLevel Default=""Debug"">
        <Override Source=""System"">Warning</Override>
    </MinimumLevel>
</Serilog>";

            LogEvent evt = null;
            using Logger log = ConfigureXml(xml)
                .WriteTo.Sink(new DelegatingSink(e => evt = e))
                .CreateLogger();

            var systemLogger = log.ForContext(Constants.SourceContextPropertyName, "System.Bar");

            log.Write(Some.InformationEvent());
            Assert.False(evt is null, "Minimum level is Debug. It should log Information messages");

            evt = null;
            systemLogger.Write(Some.InformationEvent());
            Assert.True(evt is null, "LoggingLevelSwitch initial level was Warning for logger System.*. It should not log Information messages for SourceContext System.Bar");

            systemLogger.Write(Some.WarningEvent());
            Assert.False(evt is null, "LoggingLevelSwitch initial level was Warning for logger System.*. It should log Warning messages for SourceContext System.Bar");
        }

        [Theory]
        [InlineData("$switch1")]
        [InlineData("switch1")]
        public void MinimumLevelOverrideSwitch(string switchName)
        {
            string xml = @$"<?xml version=""1.0"" standalone=""yes"" ?>
<Serilog>
    <LevelSwitches>
        <Switch Name=""{switchName}"" Level=""Warning"" />
    </LevelSwitches>
    <MinimumLevel Default=""Debug"">
        <Override Source=""System"" ControlledBy=""{switchName}"" />
    </MinimumLevel>
</Serilog>";

            LogEvent evt = null;
            using Logger log = ConfigureXml(xml)
                .WriteTo.Sink(new DelegatingSink(e => evt = e))
                .CreateLogger();

            var systemLogger = log.ForContext(Constants.SourceContextPropertyName, "System.Bar");

            log.Write(Some.InformationEvent());
            Assert.False(evt is null, "Minimum level is Debug. It should log Information messages");

            evt = null;
            systemLogger.Write(Some.InformationEvent());
            Assert.True(evt is null, "LoggingLevelSwitch initial level was Warning for logger System.*. It should not log Information messages for SourceContext System.Bar");

            systemLogger.Write(Some.WarningEvent());
            Assert.False(evt is null, "LoggingLevelSwitch initial level was Warning for logger System.*. It should log Warning messages for SourceContext System.Bar");
        }

        #endregion
    }
}
