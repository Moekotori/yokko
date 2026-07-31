using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Yokko.Import;

namespace Yokko.Desktop;

internal static class StartupFileArguments
{
    public static string[] Resolve(IEnumerable<string> arguments)
    {
        if (arguments == null)
            return [];

        return arguments
               .Where(static argument => !string.IsNullOrWhiteSpace(argument))
               .Select(static argument => argument.Trim().Trim('"'))
               .Select(tryGetFullPath)
               .Where(static path => path != null
                                     && File.Exists(path)
                                     && KnownChartImporters.CanImport(path))
               .Distinct(StringComparer.OrdinalIgnoreCase)
               .ToArray();
    }

    private static string tryGetFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or NotSupportedException
            or PathTooLongException)
        {
            return null;
        }
    }
}
