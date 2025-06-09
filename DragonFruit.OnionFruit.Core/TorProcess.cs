// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DragonFruit.OnionFruit.Core.Config;
using Microsoft.Extensions.Logging;

namespace DragonFruit.OnionFruit.Core
{
    public interface ITorProcessInformation
    {
        /// <summary>
        /// Gets the version of the running client.
        /// This is set when the process is started and reports the version in the output
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Gets the current state of the process, including if it's running, bootstrapping or stopped
        /// </summary>
        TorProcess.State ProcessState { get; }

        /// <summary>
        /// When <see cref="ProcessState"/> is bootstrapping, this is the current progress of the client
        /// </summary>
        int BootstrapProgress { get; }

        /// <summary>
        /// Event raised when the process state changes
        /// </summary>
        event EventHandler<TorProcess.State> ProcessStateChanged;

        /// <summary>
        /// Event raised when the bootstrap progress changes
        /// </summary>
        event EventHandler<int> BootstrapProgressChanged;
    }

    public class TorProcess(string torPath, ILogger logger = null) : ITorProcessInformation
    {
        private Process _process;
        private string _tempConfigFile;

        private State _processState = State.Stopped;
        private int _bootstrapProgress;

        // why is this not a standard property?
        private static readonly Encoding UTF8NoBom = new UTF8Encoding(false);

        /// <inheritdoc/>
        public string Version { get; private set; }

        /// <inheritdoc/>
        public State ProcessState
        {
            get => _processState;
            private set
            {
                if (_processState == value) return;

                _processState = value;
                ProcessStateChanged?.Invoke(this, value);
            }
        }

        /// <inheritdoc/>
        public int BootstrapProgress
        {
            get => _bootstrapProgress;
            private set
            {
                if (_bootstrapProgress == value) return;

                _bootstrapProgress = value;
                BootstrapProgressChanged?.Invoke(this, value);
            }
        }

        /// <inheritdoc/>
        public event EventHandler<State> ProcessStateChanged;

        /// <inheritdoc/>
        public event EventHandler<int> BootstrapProgressChanged;

        /// <summary>
        /// Starts the Tor process asynchronously using the given config entries
        /// </summary>
        /// <remarks>
        /// This method writes a temporary config file and starts the process using that.
        /// On exit, the file is deleted.
        /// </remarks>
        /// <param name="configEntries">The <see cref="TorrcConfigEntry"/> items to use when building the torrc file</param>
        public Task StartProcessWithConfig(params TorrcConfigEntry[] configEntries) => StartProcessWithConfig(configEntries.AsEnumerable());

        /// <summary>
        /// Starts the Tor process asynchronously using the given config entries
        /// </summary>
        /// <remarks>
        /// This method writes a temporary config file and starts the process using that.
        /// On exit, the file is deleted.
        /// </remarks>
        /// <param name="configEntries">The <see cref="TorrcConfigEntry"/> items to use when building the torrc file</param>
        public async Task StartProcessWithConfig(IEnumerable<TorrcConfigEntry> configEntries)
        {
            if (_process != null || _tempConfigFile != null)
            {
                throw new InvalidOperationException("Tor process is already running. It must be stopped before starting again");
            }

            _tempConfigFile = Path.GetTempFileName();

            // it would have been nice to create this with DeleteOnClose set, but it doesn't work due to how file sharing works
            await using (var writer = new StreamWriter(new FileStream(_tempConfigFile, FileMode.Truncate, FileAccess.Write, FileShare.None, 1024, FileOptions.Asynchronous), UTF8NoBom))
            {
                foreach (var entry in configEntries)
                {
                    await entry.WriteAsync(writer).ConfigureAwait(false);
                }
            }

            StartProcess(_tempConfigFile);
        }

        /// <summary>
        /// Starts the Tor process asynchronously
        /// </summary>
        /// <param name="configFile"></param>
        /// <exception cref="InvalidOperationException">The tor process was not cleaned up previously.</exception>
        public void StartProcess(string configFile)
        {
            if (_process != null)
            {
                throw new InvalidOperationException("Tor process is already running. It must be stopped before starting again");
            }

            var process = new Process
            {
                EnableRaisingEvents = true,
                StartInfo = new ProcessStartInfo(torPath)
                {
                    WorkingDirectory = Path.GetDirectoryName(torPath),

                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            _process = process;

            if (!string.IsNullOrEmpty(configFile) && File.Exists(configFile))
            {
                process.StartInfo.Arguments = $"-f \"{configFile}\"";
            }

            process.Exited += ProcessExited;
            process.OutputDataReceived += ProcessOutput;

            process.Start();
            ProcessState = State.Started;

            try
            {
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();
            }
            catch (Exception) when (process.HasExited)
            {
                // not a lot can be done here, the exited event will have been raised already
            }
        }

        public void StopProcess()
        {
            if (_process == null)
            {
                return;
            }

            // unsubscribe before closing the window to prevent kill state from being set.
            _process.Exited -= ProcessExited;

            if (!_process.HasExited)
            {
                try
                {
                    _process.Kill();
                }
                catch (InvalidOperationException)
                {
                    // the process no longer exists so there's nothing to kill...
                    logger?.Log(LogLevel.Information, "Could not kill tor process because it no longer exists");
                }
                catch (ExternalException ext)
                {
                    logger?.Log(LogLevel.Error, ext, "Failed to kill tor process due to a system-level error: {message}", ext.Message);
                }
                catch (Exception e)
                {
                    logger?.Log(LogLevel.Critical, e, "Failed to kill tor process: {message}", e.Message);
                    throw;
                }
            }

            _process.OutputDataReceived -= ProcessOutput;
            _process.Dispose();

            if (_tempConfigFile != null)
            {
                try
                {
                    File.Delete(_tempConfigFile);
                }
                catch (Exception e)
                {
                    logger?.Log(LogLevel.Information, e, "Failed to delete temporary config file ({name}): {msg}", Path.GetFileName(_tempConfigFile), e.Message);
                }
            }

            _process = null;
            _tempConfigFile = null;

            Version = null;

            if (ProcessState != State.Killed)
            {
                ProcessState = State.Stopped;
            }
        }

        private void ProcessOutput(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data))
            {
                return;
            }

            // check for version (should be first line of output when starting)
            if (string.IsNullOrEmpty(Version))
            {
                var versionMatch = TorProcessOutputRegex.VersionOutput().Match(e.Data);

                if (versionMatch.Success)
                {
                    Version = versionMatch.Groups["version"].Value;
                    return;
                }
            }

            // check if it's a bootstrap message
            var logOutput = TorProcessOutputRegex.ConsoleLogOutput().Match(e.Data);

            if (!logOutput.Success)
            {
                logger?.Log(LogLevel.Debug, "[{procName}]: {output}", _process.ProcessName, e.Data);
                return;
            }

            if (logger != null)
            {
                var priority = logOutput.Groups["priority"].Value switch
                {
                    "notice" or "info" => LogLevel.Information,
                    "warn" => LogLevel.Warning,
                    "err" => LogLevel.Error,

                    _ => LogLevel.Debug
                };

                logger?.Log(priority, "[{procName}]: {output}", _process.ProcessName, logOutput.Groups["message"].Value);
            }

            // check if it's a bootstrap message
            var bootstrapOutput = TorProcessOutputRegex.BootstrapLogOutput().Match(logOutput.Groups["message"].Value);

            if (bootstrapOutput.Success)
            {
                BootstrapProgress = int.Parse(bootstrapOutput.Groups["progress"].Value);
                ProcessState = BootstrapProgress == 100 ? State.Running : State.Bootstrapping;
            }
        }

        private void ProcessExited(object sender, EventArgs e)
        {
            // when we kill the process, we ensure this is unsubscribed from
            // this should only be called when the process exits unexpectedly.
            ProcessState = State.Killed;

            // as the process is dead might as well clean up now
            StopProcess();
        }

        public enum State
        {
            /// <summary>
            /// The process has been started but has not reported any current status
            /// </summary>
            Started,

            /// <summary>
            /// The client is bootstrapping the connection to the network
            /// </summary>
            Bootstrapping,

            /// <summary>
            /// The client is connected and bootstrapped
            /// </summary>
            Running,

            /// <summary>
            /// The client is not running (if it was shutdown, it was done so by the owning <see cref="TorProcess"/>)
            /// </summary>
            Stopped,

            /// <summary>
            /// The process was killed by an external source. <see cref="StopProcess"/> will be called automatically.
            /// </summary>
            Killed
        }
    }
}