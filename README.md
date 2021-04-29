# Serilog.Settings.XML2

A Serilog settings provider that reads from XML sources, like `XML files` or [XElement](https://docs.microsoft.com/de-de/dotnet/api/system.xml.linq.xelement). This is a fork of the [Serilog.Settings.Configuration](https://github.com/serilog/serilog-settings-configuration) and supoorts most of its use cases, but with much less dependencies.

By default, the configuration is read from the root element of a `XML file`.
```XML
<Serilog>
  <Using>Serilog.Sinks.Console</Using>
  <Using>Serilog.Sinks.File</Using>
  <MinimumLevel>Debug</MinimumLevel>
  <WriteTo Name="Console" />
  <WriteTo Name="File">
    <Path>Logs/log.txt</Path>
  </WriteTo>
  <Enrich Name="FromLogContext" />
  <Enrich Name="WithMachineName" />
  <Enrich Name="WithThreadId" />
  <Destructure Name="With">
    <Policy>Sample.CustomPolicy, Sample</Policy>
  </Destructure>
  <Destructure Name="ToMaximumDepth">
    <MaximumDestructuringDepth>4</MaximumDestructuringDepth>
  </Destructure>
  <Destructure Name="ToMaximumStringLength">
    <MaximumStringLength>100</MaximumStringLength>
  </Destructure>
  <Destructure Name="ToMaximumCollectionCount">
    <MaximumCollectionCount>10</MaximumCollectionCount>
  </Destructure>
  <Property Name="Application" Value="Sample" />
</Serilog>
```

After installing this package, use `ReadFrom.XML()` and pass a path to a `XML file` or a [XElement](https://docs.microsoft.com/de-de/dotnet/api/system.xml.linq.xelement) object as the root element to read from.

```csharp
static void Main(string[] args)
{
    var logger = new LoggerConfiguration()
        .ReadFrom.XML("config.xml")
        .CreateLogger();

    logger.Information("Hello, world!");
}
```

This example relies on the _[Serilog.Sinks.Console](https://github.com/serilog/serilog-sinks-console)_, _[Serilog.Sinks.File](https://github.com/serilog/serilog-sinks-file)_, _[Serilog.Enrichers.Environment](https://github.com/serilog/serilog-enrichers-environment)_ and _[Serilog.Enrichers.Thread](https://github.com/serilog/serilog-enrichers-thread)_ packages also being installed.

# Syntax and structure
Please have a look in the [Wiki](https://github.com/KhaosCoders/serilog-settings-xml/wiki) for information about the XML structure and capabilities of this provider

# Acknowledgement
This project depends heavily on the sources of [Serilog.Settings.Configuration](https://github.com/serilog/serilog-settings-configuration)