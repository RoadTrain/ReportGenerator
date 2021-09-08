using System;

namespace Palmmedia.ReportGenerator.Core.Logging
{
    /// <summary>
    /// Logger delegate which consumes log messages.
    /// </summary>
    /// <param name="verbosityLevel">Message verbosity level.</param>
    /// <param name="message">The message or format string.</param>
    /// <param name="args">(optional) Format string arguments.</param>
    public delegate void LogDelegate(VerbosityLevel verbosityLevel, string message, object[] args = null);

    /// <summary>
    /// <see cref="ILogger"/> implementation that sends messages to a given <see cref="LogDelegate"/>.
    /// </summary>
    internal class DelegateLogger : ILogger
    {
        private readonly LogDelegate logDelegate;

        /// <summary>
        /// Initializes a new instance of the <see cref="DelegateLogger"/> class.
        /// </summary>
        /// <param name="logDelegate">Delegate to be used for consuming log messages.</param>
        public DelegateLogger(LogDelegate logDelegate)
        {
            this.logDelegate = logDelegate ?? throw new ArgumentNullException(nameof(logDelegate));
        }

        /// <summary>
        /// Gets or sets the verbosity of delegate loggers.
        /// </summary>
        public VerbosityLevel VerbosityLevel { get; set; }

        /// <summary>
        /// Log a message at DEBUG level.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Debug(string message)
        {
            if (this.VerbosityLevel < VerbosityLevel.Info)
            {
                this.logDelegate.Invoke(VerbosityLevel.Verbose, message);
            }
        }

        /// <summary>
        /// Log a formatted message at DEBUG level.
        /// </summary>
        /// <param name="format">The template string.</param>
        /// <param name="args">The arguments.</param>
        public void DebugFormat(string format, params object[] args)
        {
            if (this.VerbosityLevel < VerbosityLevel.Info)
            {
                this.logDelegate.Invoke(VerbosityLevel.Verbose, format, args);
            }
        }

        /// <summary>
        /// Log a message at INFO level.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Info(string message)
        {
            if (this.VerbosityLevel < VerbosityLevel.Warning)
            {
                this.logDelegate.Invoke(VerbosityLevel.Info, message);
            }
        }

        /// <summary>
        /// Log a formatted message at INFO level.
        /// </summary>
        /// <param name="format">The template string.</param>
        /// <param name="args">The arguments.</param>
        public void InfoFormat(string format, params object[] args)
        {
            if (this.VerbosityLevel < VerbosityLevel.Warning)
            {
                this.logDelegate.Invoke(VerbosityLevel.Info, format, args);
            }
        }

        /// <summary>
        /// Log a message at WARN level.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Warn(string message)
        {
            if (this.VerbosityLevel < VerbosityLevel.Error)
            {
                this.logDelegate.Invoke(VerbosityLevel.Warning, message);
            }
        }

        /// <summary>
        /// Log a formatted message at WARN level.
        /// </summary>
        /// <param name="format">The template string.</param>
        /// <param name="args">The arguments.</param>
        public void WarnFormat(string format, params object[] args)
        {
            if (this.VerbosityLevel < VerbosityLevel.Error)
            {
                this.logDelegate.Invoke(VerbosityLevel.Warning, format, args);
            }
        }

        /// <summary>
        /// Log a message at INFO level.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Error(string message)
        {
            if (this.VerbosityLevel < VerbosityLevel.Off)
            {
                this.logDelegate.Invoke(VerbosityLevel.Error, message);
            }
        }

        /// <summary>
        /// Log a formatted message at ERROR level.
        /// </summary>
        /// <param name="format">The template string.</param>
        /// <param name="args">The arguments.</param>
        public void ErrorFormat(string format, params object[] args)
        {
            if (this.VerbosityLevel < VerbosityLevel.Off)
            {
                this.logDelegate.Invoke(VerbosityLevel.Error, format, args);
            }
        }
    }
}
