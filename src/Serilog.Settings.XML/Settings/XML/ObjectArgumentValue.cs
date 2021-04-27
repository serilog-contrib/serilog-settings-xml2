using Serilog.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace Serilog.Settings.XML
{
    internal class ObjectArgumentValue : IConfigurationArgumentValue
    {
        private readonly XElement _section;
        private readonly IReadOnlyCollection<Assembly> _configurationAssemblies;

        public ObjectArgumentValue(XElement section, IReadOnlyCollection<Assembly> configurationAssemblies)
        {
            _section = section ?? throw new ArgumentNullException(nameof(section));

            // used by nested logger configurations to feed a new pass by XmlReader
            _configurationAssemblies = configurationAssemblies ?? throw new ArgumentNullException(nameof(configurationAssemblies));
        }

        public object ConvertTo(Type toType, ResolutionContext resolutionContext)
        {
            // return the entire section for internal processing
            if (toType == typeof(XElement)) return _section;

            // process a nested configuration to populate an Action<> logger/sink config parameter?
            var typeInfo = toType.GetTypeInfo();
            if (typeInfo.IsGenericType &&
                typeInfo.GetGenericTypeDefinition() is Type genericType && genericType == typeof(Action<>))
            {
                var configType = typeInfo.GenericTypeArguments[0];
                IXmlReader configReader = new XmlReader(_section, _configurationAssemblies, resolutionContext);

                return configType switch
                {
                    _ when configType == typeof(LoggerConfiguration) => new Action<LoggerConfiguration>(configReader.Configure),
                    _ when configType == typeof(LoggerSinkConfiguration) => new Action<LoggerSinkConfiguration>(configReader.ApplySinks),
                    _ when configType == typeof(LoggerEnrichmentConfiguration) => new Action<LoggerEnrichmentConfiguration>(configReader.ApplyEnrichment),
                    _ => throw new ArgumentException($"Configuration resolution for Action<{configType.Name}> parameter type at the element {_section.Name} is not implemented.")
                };
            }

            if (toType.IsArray)
                return CreateArray();

            if (IsContainer(toType, out var elementType) && TryCreateContainer(out var result))
                return result;

            return Convert.ChangeType(_section, toType);

            object CreateArray()
            {
                var elementType = toType.GetElementType();
                var configurationElements = _section.Elements().ToArray();
                var result = Array.CreateInstance(elementType, configurationElements.Length);
                for (int i = 0; i < configurationElements.Length; ++i)
                {
                    var argumentValue = XmlReader.GetArgumentValue(configurationElements[i], _configurationAssemblies);
                    var value = argumentValue.ConvertTo(elementType, resolutionContext);
                    result.SetValue(value, i);
                }

                return result;
            }

            bool TryCreateContainer(out object result)
            {
                result = null;

                if (toType.GetConstructor(Type.EmptyTypes) == null)
                    return false;

                // https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/object-and-collection-initializers#collection-initializers
                var addMethod = toType.GetMethods().FirstOrDefault(m => !m.IsStatic && m.Name == "Add" && m.GetParameters()?.Length == 1 && m.GetParameters()[0].ParameterType == elementType);
                if (addMethod == null)
                    return false;

                var configurationElements = _section.Elements().ToArray();
                result = Activator.CreateInstance(toType);

                for (int i = 0; i < configurationElements.Length; ++i)
                {
                    var argumentValue = XmlReader.GetArgumentValue(configurationElements[i], _configurationAssemblies);
                    var value = argumentValue.ConvertTo(elementType, resolutionContext);
                    addMethod.Invoke(result, new object[] { value });
                }

                return true;
            }
        }

        private static bool IsContainer(Type type, out Type elementType)
        {
            Type iface = Array.Find(type.GetInterfaces(),
                i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            if (iface != null)
            {
                elementType = iface.GetGenericArguments()[0];
                return true;
            }

            elementType = null;
            return false;
        }
    }
}
