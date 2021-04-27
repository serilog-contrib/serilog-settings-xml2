using System;

namespace Serilog.Settings.XML
{
    interface IConfigurationArgumentValue
    {
        object ConvertTo(Type toType, ResolutionContext resolutionContext);
    }
}
