using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Yokko.Desktop.Diagnostics
{
    /// <summary>
    /// Captures fatal managed exceptions and writes a self-contained diagnostic
    /// report without interfering with the original crash.
    /// </summary>
    internal sealed class CrashReportHandler : IDisposable
    {
        private const int report_format_version = 1;

        private readonly Assembly applicationAssembly;
        private string reportDirectory;
        private string frameworkLogDirectory;
        private int reportSequence;
        private bool isDisposed;

        public CrashReportHandler(Assembly applicationAssembly, string reportDirectory = null)
        {
            this.applicationAssembly = applicationAssembly
                                       ?? throw new ArgumentNullException(nameof(applicationAssembly));
            this.reportDirectory = reportDirectory ?? getFallbackReportDirectory();

            AppDomain.CurrentDomain.UnhandledException += onUnhandledException;
        }

        /// <summary>
        /// Moves future reports into the host's user storage once it is available.
        /// </summary>
        public void SetStoragePaths(string crashReports, string frameworkLogs)
        {
            if (string.IsNullOrWhiteSpace(crashReports))
                throw new ArgumentException("A crash report directory is required.", nameof(crashReports));

            reportDirectory = crashReports;
            frameworkLogDirectory = frameworkLogs;
        }

        /// <summary>
        /// Tries to synchronously persist a fatal exception. No reporting failure
        /// is allowed to replace or hide the original exception.
        /// </summary>
        public string TryWrite(Exception exception, string source, bool isTerminating = true)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            try
            {
                Directory.CreateDirectory(reportDirectory);

                DateTimeOffset timestamp = DateTimeOffset.UtcNow;
                string reportPath = createUniqueReportPath(timestamp);
                string contents = buildReport(exception, source, isTerminating, timestamp);

                using var stream = new FileStream(
                    reportPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.Read);
                using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                writer.Write(contents);
                writer.Flush();
                stream.Flush(true);

                return reportPath;
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;
            AppDomain.CurrentDomain.UnhandledException -= onUnhandledException;
        }

        private void onUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            if (args.ExceptionObject is Exception exception)
            {
                TryWrite(exception, "AppDomain.UnhandledException", args.IsTerminating);
                return;
            }

            TryWrite(
                new InvalidOperationException(
                    $"A non-Exception object was reported as unhandled: {safeToString(args.ExceptionObject)}"),
                "AppDomain.UnhandledException",
                args.IsTerminating);
        }

        private string buildReport(
            Exception exception,
            string source,
            bool isTerminating,
            DateTimeOffset timestamp)
        {
            var report = new StringBuilder(8192);
            AssemblyName assemblyName = applicationAssembly.GetName();
            string informationalVersion = applicationAssembly
                                          .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                                          ?.InformationalVersion;
            Thread thread = Thread.CurrentThread;

            report.AppendLine("Yokko crash report");
            report.AppendLine("==================");
            appendValue(report, "Report format", report_format_version);
            appendValue(report, "Timestamp (UTC)", timestamp.ToString("O", CultureInfo.InvariantCulture));
            appendValue(report, "Timestamp (local)", timestamp.ToLocalTime().ToString("O", CultureInfo.InvariantCulture));
            appendValue(report, "Source", source);
            appendValue(report, "Process terminating", isTerminating);

            appendSection(report, "Application");
            appendValue(report, "Name", assemblyName.Name);
            appendValue(report, "Version", assemblyName.Version);
            appendValue(report, "Informational version", informationalVersion);
            appendValue(report, "Executable", safe(() => Environment.ProcessPath));
            appendValue(report, "Base directory", safe(() => AppContext.BaseDirectory));
            appendValue(report, "Current directory", safe(() => Environment.CurrentDirectory));
            appendValue(report, "Process ID", safe(() => Environment.ProcessId.ToString(CultureInfo.InvariantCulture)));
            appendValue(report, "Process uptime", safe(getProcessUptime));

            appendSection(report, "Runtime");
            appendValue(report, "Operating system", safe(() => RuntimeInformation.OSDescription));
            appendValue(report, "OS architecture", safe(() => RuntimeInformation.OSArchitecture.ToString()));
            appendValue(report, "Process architecture", safe(() => RuntimeInformation.ProcessArchitecture.ToString()));
            appendValue(report, "Framework", safe(() => RuntimeInformation.FrameworkDescription));
            appendValue(report, "Runtime identifier", safe(() => RuntimeInformation.RuntimeIdentifier));
            appendValue(report, "64-bit process", safe(() => Environment.Is64BitProcess.ToString()));
            appendValue(report, "Processor count", safe(() => Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture)));
            appendValue(report, "Working set", safe(() => Environment.WorkingSet.ToString(CultureInfo.InvariantCulture)));
            appendValue(report, "Managed memory", safe(() => GC.GetTotalMemory(false).ToString(CultureInfo.InvariantCulture)));
            appendValue(report, "Culture", safe(() => CultureInfo.CurrentCulture.Name));
            appendValue(report, "UI culture", safe(() => CultureInfo.CurrentUICulture.Name));
            appendValue(report, "Time zone", safe(() => TimeZoneInfo.Local.Id));

            appendSection(report, "Thread");
            appendValue(report, "Managed thread ID", thread.ManagedThreadId);
            appendValue(report, "Name", thread.Name);
            appendValue(report, "Background", thread.IsBackground);
            appendValue(report, "Thread pool", thread.IsThreadPoolThread);
            appendValue(report, "Apartment state", safe(() => thread.GetApartmentState().ToString()));

            appendSection(report, "Diagnostic paths");
            appendValue(report, "Crash reports", reportDirectory);
            appendValue(report, "Framework logs", frameworkLogDirectory);

            appendSection(report, "Exception");
            appendException(report, exception, 0);

            return report.ToString();
        }

        private static void appendException(StringBuilder report, Exception exception, int depth)
        {
            if (depth >= 16)
            {
                report.AppendLine("[Further inner exceptions omitted]");
                return;
            }

            string prefix = depth == 0 ? "Root" : $"Inner {depth}";
            appendValue(report, $"{prefix} type", exception.GetType().FullName);
            appendValue(report, $"{prefix} message", safe(() => exception.Message));
            appendValue(report, $"{prefix} HResult", $"0x{exception.HResult:X8}");
            appendValue(report, $"{prefix} source", safe(() => exception.Source));
            appendValue(report, $"{prefix} target", safe(() => exception.TargetSite?.ToString()));

            appendExceptionData(report, exception, prefix);

            report.AppendLine($"{prefix} stack trace:");
            report.AppendLine(safe(() => exception.StackTrace) ?? "(not available)");

            if (exception is AggregateException aggregateException)
            {
                for (int i = 0; i < aggregateException.InnerExceptions.Count; i++)
                {
                    report.AppendLine();
                    report.AppendLine($"Aggregate child {i + 1}:");
                    appendException(report, aggregateException.InnerExceptions[i], depth + 1);
                }

                return;
            }

            if (exception.InnerException != null)
            {
                report.AppendLine();
                appendException(report, exception.InnerException, depth + 1);
            }
        }

        private static void appendExceptionData(StringBuilder report, Exception exception, string prefix)
        {
            IDictionary data;

            try
            {
                data = exception.Data;
            }
            catch
            {
                return;
            }

            if (data == null || data.Count == 0)
                return;

            report.AppendLine($"{prefix} data:");

            try
            {
                foreach (DictionaryEntry entry in data)
                {
                    report.Append("  ");
                    report.Append(safeToString(entry.Key));
                    report.Append(" = ");
                    report.AppendLine(safeToString(entry.Value));
                }
            }
            catch (Exception dataException)
            {
                report.AppendLine($"  (could not enumerate: {dataException.GetType().FullName})");
            }
        }

        private string createUniqueReportPath(DateTimeOffset timestamp)
        {
            while (true)
            {
                int sequence = Interlocked.Increment(ref reportSequence);
                string fileName = string.Format(
                    CultureInfo.InvariantCulture,
                    "crash-{0:yyyyMMdd'T'HHmmss.fff'Z'}-p{1}-{2}.txt",
                    timestamp,
                    Environment.ProcessId,
                    sequence);
                string path = Path.Combine(reportDirectory, fileName);

                if (!File.Exists(path))
                    return path;
            }
        }

        private static string getFallbackReportDirectory()
        {
            string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            if (string.IsNullOrWhiteSpace(localData))
                localData = Path.GetTempPath();

            return Path.Combine(localData, "Yokko", "crashes");
        }

        private static string getProcessUptime()
        {
            using Process process = Process.GetCurrentProcess();
            TimeSpan uptime = DateTime.Now - process.StartTime;
            return uptime.ToString("c", CultureInfo.InvariantCulture);
        }

        private static string safe(Func<string> value)
        {
            try
            {
                return value();
            }
            catch (Exception exception)
            {
                return $"(unavailable: {exception.GetType().FullName})";
            }
        }

        private static string safeToString(object value)
        {
            try
            {
                return value?.ToString() ?? "(null)";
            }
            catch (Exception exception)
            {
                return $"(ToString failed: {exception.GetType().FullName})";
            }
        }

        private static void appendSection(StringBuilder report, string title)
        {
            report.AppendLine();
            report.AppendLine(title);
            report.AppendLine(new string('-', title.Length));
        }

        private static void appendValue(StringBuilder report, string name, object value) =>
            report.AppendLine($"{name}: {value ?? "(not available)"}");
    }
}
