using System;

namespace Palmmedia.ReportGenerator.Core.Logging
{
    /// <summary>
    /// A logger factory creating delegate loggers.
    /// </summary>
    internal class DelegateLoggerFactory : ILoggerFactory
    {
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DelegateLoggerFactory"/> class.
        /// </summary>
        /// <param name="logDelegate">Delegate to be used for consuming log messages.</param>
        public DelegateLoggerFactory(LogDelegate logDelegate)
        {
            this.logger = new DelegateLogger(logDelegate);
        }

        /// <inheritdoc/>
        public VerbosityLevel VerbosityLevel
        {
            get => this.logger.VerbosityLevel;
            set => this.logger.VerbosityLevel = value;
        }

        /// <inheritdoc/>
        public ILogger GetLogger(Type type) => this.logger;
    }
}
