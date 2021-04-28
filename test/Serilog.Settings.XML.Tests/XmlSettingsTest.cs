using Serilog.Core;
using Serilog.Enrichers;
using Serilog.Events;
using Serilog.Settings.XML.Tests.Support;
using System;
using System.Xml.Linq;
using TestDummies;
using Xunit;

namespace Serilog.Settings.XML.Tests
{
    public class XmlSettingsTest
    {
#if NET452
        const string FilterLib = "Serilog.Filters.Expressions";
#else
        const string FilterLib = "Serilog.Expressions";
#endif

        private static LoggerConfiguration ConfigureXml(string xml)
        {
            XElement xElement = XElement.Parse(xml);
            return new LoggerConfiguration()
                .ReadFrom.Xml(xElement);
        }

        #region Sinks

        [Fact]
        public void SinksAreConfigured()
        {
            const string xml = @"<?xml version=""1.0"" standalone=""yes"" ?>
<Serilog>
    <Using>TestDummies</Using>
    <WriteTo Name=""DummyRollingFile"">
        <PathFormat>C:\</PathFormat>
    </WriteTo>
</Serilog>";

            using var log = ConfigureXml(xml)
                .CreateLogger();

            DummyRollingFileSink.Reset();
            DummyRollingFileAuditSink.Reset();

            log.Write(Some.InformationEvent());

            Assert.Single(DummyRollingFileSink.Emitted);
            Assert.Empty(DummyRollingFileAuditSink.Emitted);
        }

        [Fact]
        public void SinkWithStringArrayArgument()
        {
            const string xml = @"<?xml version=""1.0"" standalone=""yes"" ?>
<Serilog>
    <Using>TestDummies</Using>
    <WriteTo Name=""DummyRollingFile"">
        <PathFormat>C:\</PathFormat>
        <StringArrayBinding>
            <Item>foo</Item>
            <Item>bar</Item>
            <Item>baz</Item>
        </StringArrayBinding>
    </WriteTo>
</Serilog>";

            using var log = ConfigureXml(xml)
                .CreateLogger();

            DummyRollingFileSink.Reset();

            log.Write(Some.InformationEvent());

            Assert.Single(DummyRollingFileSink.Emitted);
        }

        [Fact]
        public void SinkWithIntArrayArgument()
        {
            const string xml = @"<?xml version=""1.0"" standalone=""yes"" ?>
<Serilog>
    <Using>TestDummies</Using>
    <WriteTo Name=""DummyRollingFile"">
        <PathFormat>C:\</PathFormat>
        <IntArrayBinding>
            <Item>1</Item>
            <Item>2</Item>
            <Item>3</Item>
        </IntArrayBinding>
    </WriteTo>
</Serilog>";

            using var log = ConfigureXml(xml)
                 .CreateLogger();

            DummyRollingFileSink.Reset();

            log.Write(Some.InformationEvent());

            Assert.Single(DummyRollingFileSink.Emitted);
        }

        [Fact]
        public void WriteToLoggerWithRestrictedToMinimumLevelIsSupported()
        {
            const string xml = @"<?xml version=""1.0"" standalone=""yes"" ?>
<Serilog>
    <Using>TestDummies</Using>
    <WriteTo Name=""Logger"">
        <ConfigureLogger>
            <WriteTo Name=""DummyRollingFile"">
                <PathFormat>C:\</PathFormat>
            </WriteTo>
        </ConfigureLogger>
        <RestrictedToMinimumLevel>Warning</RestrictedToMinimumLevel>
    </WriteTo>
</Serilog>";

            using var log = ConfigureXml(xml)
                .CreateLogger();

            DummyRollingFileSink.Reset();

            log.Write(Some.InformationEvent());
            log.Write(Some.WarningEvent());

            Assert.Single(DummyRollingFileSink.Emitted);
        }

        #endregion

        #region Destructures

        private static string GetDestructuredProperty(object x, string xml)
        {
            LogEvent evt = null;
            using var log = ConfigureXml(xml)
                .WriteTo.Sink(new DelegatingSink(e => evt = e))
                .CreateLogger();
            log.Information("{@X}", x);
            var result = evt.Properties["X"].ToString();
            return result;
        }

        [Fact]
        public void DestructureLimitsNestingDepth()
        {
            const string xml = @"<?xml version=""1.0"" standalone=""yes"" ?>
<Serilog>
    <Destructure Name=""ToMaximumDepth"">
        <MaximumDestructuringDepth>3</MaximumDestructuringDepth>
    </Destructure>
</Serilog>";

            var NestedObject = new
            {
                A = new
                {
                    B = new
                    {
                        C = new
                        {
                            D = "F"
                        }
                    }
                }
            };

            var msg = GetDestructuredProperty(NestedObject, xml);

            Assert.Contains("C", msg);
            Assert.DoesNotContain("D", msg);
        }

        [Fact]
        public void DestructureLimitsStringLength()
        {
            const string xml = @"<?xml version=""1.0"" standalone=""yes"" ?>
<Serilog>
    <Destructure Name=""ToMaximumStringLength"">
        <MaximumStringLength>3</MaximumStringLength>
    </Destructure>
</Serilog>";

            var inputString = "ABCDEFGH";
            var msg = GetDestructuredProperty(inputString, xml);

            Assert.Equal("\"AB…\"", msg);
        }

        [Fact]
        public void DestructureLimitsCollectionCount()
        {
            const string xml = @"<?xml version=""1.0"" standalone=""yes"" ?>
<Serilog>
    <Destructure Name=""ToMaximumCollectionCount"">
        <MaximumCollectionCount>3</MaximumCollectionCount>
    </Destructure>
</Serilog>";

            var collection = new[] { 1, 2, 3, 4, 5, 6 };
            var msg = GetDestructuredProperty(collection, xml);

            Assert.Contains("3", msg);
            Assert.DoesNotContain("4", msg);
        }

        [Fact]
        public void DestructuringAsScalarIsAppliedWithShortTypeName()
        {
            const string xml = @"<?xml version=""1.0"" standalone=""yes"" ?>
<Serilog>
    <Destructure Name=""AsScalar"">
        <ScalarType>System.Version</ScalarType>
    </Destructure>
</Serilog>";

            LogEvent evt = null;
            using var log = ConfigureXml(xml)
                .WriteTo.Sink(new DelegatingSink(e => evt = e))
                .CreateLogger();

            log.Information("Destructuring as scalar {@Scalarized}", new Version(2, 3));
            var prop = evt.Properties["Scalarized"];

            Assert.IsType<ScalarValue>(prop);
        }

        [Fact]
        public void DestructuringAsScalarIsAppliedWithAssemblyQualifiedName()
        {
            string xml = @$"<?xml version=""1.0"" standalone=""yes"" ?>
<Serilog>
    <Destructure Name=""AsScalar"">
        <ScalarType>{typeof(Version).AssemblyQualifiedName}</ScalarType>
    </Destructure>
</Serilog>";

            LogEvent evt = null;
            using var log = ConfigureXml(xml)
                .WriteTo.Sink(new DelegatingSink(e => evt = e))
                .CreateLogger();

            log.Information("Destructuring as scalar {@Scalarized}", new Version(2, 3));
            var prop = evt.Properties["Scalarized"];

            Assert.IsType<ScalarValue>(prop);
        }

        #endregion

        #region Filters

        [Fact]
        public void FilterExpression()
        {
            string xml = @$"<?xml version=""1.0"" standalone=""yes"" ?>
<Serilog>
    <Using>{FilterLib}</Using>
    <Filter Name=""ByIncludingOnly"">
        <Expression>Prop = 42</Expression>
    </Filter>
</Serilog>";

            LogEvent evt = null;
            using Logger log = ConfigureXml(xml)
                .WriteTo.Sink(new DelegatingSink(e => evt = e))
                .CreateLogger();

            log.ForContext("Prop", 99).Write(Some.ErrorEvent());
            Assert.True(evt is null, "Filter for Prop is set. Message should not be logged");

            log.ForContext("Prop", 42).Write(Some.ErrorEvent());
            Assert.True(evt != null, "Filter for Prop is set. Message should be logged");
        }

        [Theory]
        [InlineData("$switch1")]
        [InlineData("switch1")]
        public void FilterExpressionSwitch(string switchName)
        {
            string xml = @$"<?xml version=""1.0"" standalone=""yes"" ?>
<Serilog>
    <Using>{FilterLib}</Using>
    <FilterSwitches>
        <Switch Name=""{switchName}"" Expression=""Prop = 42"" />
    </FilterSwitches>
    <Filter Name=""ControlledBy"">
        <Switch>{switchName}</Switch>
    </Filter>
</Serilog>";

            LogEvent evt = null;
            using Logger log = ConfigureXml(xml)
                .WriteTo.Sink(new DelegatingSink(e => evt = e))
                .CreateLogger();

            log.ForContext("Prop", 99).Write(Some.ErrorEvent());
            Assert.True(evt is null, "Filter for Prop is set. Message should not be logged");

            log.ForContext("Prop", 42).Write(Some.ErrorEvent());
            Assert.True(evt != null, "Filter for Prop is set. Message should be logged");
        }

        [Theory]
        [InlineData("$switch1")]
        [InlineData("switch1")]
        public void FilterExpressionSwitchShort(string switchName)
        {
            string xml = @$"<?xml version=""1.0"" standalone=""yes"" ?>
<Serilog>
    <Using>{FilterLib}</Using>
    <FilterSwitches>
        <Switch Name=""{switchName}"" Expression=""Prop = 42"" />
    </FilterSwitches>
    <Filter ControlledBy=""{switchName}"" />
</Serilog>";

            LogEvent evt = null;
            using Logger log = ConfigureXml(xml)
                .WriteTo.Sink(new DelegatingSink(e => evt = e))
                .CreateLogger();

            log.ForContext("Prop", 99).Write(Some.ErrorEvent());
            Assert.True(evt is null, "Filter for Prop is set. Message should not be logged");

            log.ForContext("Prop", 42).Write(Some.ErrorEvent());
            Assert.True(evt != null, "Filter for Prop is set. Message should be logged");
        }

        #endregion

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
        <Switch Name=""{switchName}"">Warning</Switch>
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
