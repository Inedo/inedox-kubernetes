using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Extensibility.Operations;
using Inedo.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Inedo.Extensions.Kubernetes
{
    internal static class Utils
    {
        public static string EscapeLinuxArg(this string arg)
        {
            if (arg.Length > 0 && arg.All(c => char.IsLetterOrDigit(c) || c == '/' || c == '-' || c == '_' || c == '.'))
            {
                return arg;
            }

            return "'" + arg.Replace("'", "'\\''") + "'";
        }

        public static string EscapeWindowsArg(this string arg)
        {
            // https://msdn.microsoft.com/en-us/library/ms880421

            if (!arg.Any(c => char.IsWhiteSpace(c) || c == '\\' || c == '"'))
            {
                return arg;
            }

            var str = new StringBuilder();
            str.Append('"');
            int slashes = 0;
            foreach (char c in arg)
            {
                if (c == '"')
                {
                    str.Append('\\', slashes);
                    str.Append("\\\"");
                    slashes = 0;
                }
                else if (c == '\\')
                {
                    str.Append('\\');
                    slashes++;
                }
                else
                {
                    str.Append(c);
                    slashes = 0;
                }
            }
            str.Append('\\', slashes);
            str.Append('"');

            return str.ToString();
        }

        internal static async Task RunKubeCtlAsync(this ILogSink log, IOperationExecutionContext context, IEnumerable<string> args, Action<string> logOutput = null, Action<string> logError = null, Func<int?, bool> handleExit = null, CancellationToken? cancellationToken = null)
        {
            var fileOps = await context.Agent.TryGetServiceAsync<ILinuxFileOperationsExecuter>() ?? await context.Agent.GetServiceAsync<IFileOperationsExecuter>();
            var procExec = await context.Agent.GetServiceAsync<IRemoteProcessExecuter>();
            var escapeArg = fileOps is ILinuxFileOperationsExecuter ? (Func<string, string>)Utils.EscapeLinuxArg : Utils.EscapeWindowsArg;

            var startInfo = new RemoteProcessStartInfo
            {
                FileName = "kubectl",
                Arguments = string.Join(" ", (args ?? new string[0]).Where(arg => arg != null).Select(escapeArg)),
                WorkingDirectory = context.WorkingDirectory
            };

            log.LogDebug($"Working directory: {startInfo.WorkingDirectory}");
            await fileOps.CreateDirectoryAsync(startInfo.WorkingDirectory);
            log.LogDebug($"Running command: {escapeArg(startInfo.FileName)} {startInfo.Arguments}");

            int? exitCode;
            using (var process = procExec.CreateProcess(startInfo))
            {
                process.OutputDataReceived += (s, e) => (logOutput ?? log.LogInformation)(e.Data);
                process.ErrorDataReceived += (s, e) => (logError ?? log.LogError)(e.Data);
                process.Start();
                await process.WaitAsync(cancellationToken ?? context.CancellationToken);
                exitCode = process.ExitCode;
            }

            if (handleExit?.Invoke(exitCode) == true)
            {
                return;
            }

            if (exitCode == 0)
            {
                log.LogInformation("Process exit code indicates success.");
                return;
            }

            log.LogError($"Process exit code indicates failure. ({AH.CoalesceString(exitCode, "(unknown)")})");

            // Command failed. Try to give a better error message if kubectl isn't even installed.
            var verifyInstalledStartInfo = new RemoteProcessStartInfo
            {
                FileName = fileOps is ILinuxFileOperationsExecuter ? "/usr/bin/which" : "System32\\where.exe",
                Arguments = escapeArg(startInfo.FileName),
                WorkingDirectory = context.WorkingDirectory
            };

            if (fileOps is ILinuxFileOperationsExecuter)
                verifyInstalledStartInfo.Arguments = "-- " + verifyInstalledStartInfo.Arguments;
            else
                verifyInstalledStartInfo.FileName = PathEx.Combine(await procExec.GetEnvironmentVariableValueAsync("SystemRoot"), verifyInstalledStartInfo.FileName);

            using (var process = procExec.CreateProcess(verifyInstalledStartInfo))
            {
                // Don't care about output.
                process.Start();
                await process.WaitAsync(cancellationToken ?? context.CancellationToken);

                // 0 = file exists, anything other than 0 or 1 = error trying to run which/where.exe
                if (process.ExitCode == 1)
                {
                    log.LogWarning("Is kubectl installed and in the PATH?");
                }
            }
        }
    }
}
