﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Abstractions;
using Kudu.Contracts.Jobs;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.Jobs
{
    public class TriggeredJobRunLogger : JobLogger
    {
        private readonly string _id;
        private readonly string _historyPath;
        private readonly string _outputFilePath;
        private readonly string _errorFilePath;

        private TriggeredJobRunLogger(string jobName, string id, IEnvironment environment, IFileSystem fileSystem, ITraceFactory traceFactory)
            : base(environment, fileSystem, traceFactory)
        {
            _id = id;

            _historyPath = Path.Combine(Environment.JobsDataPath, Constants.TriggeredPath, jobName, _id);
            FileSystemHelpers.EnsureDirectory(_historyPath);

            _outputFilePath = Path.Combine(_historyPath, "output.log");
            _errorFilePath = Path.Combine(_historyPath, "error.log");
        }

        [SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters", Justification = "We do not want to accept jobs which are not TriggeredJob")]
        public static TriggeredJobRunLogger LogNewRun(TriggeredJob triggeredJob, IEnvironment environment, IFileSystem fileSystem, ITraceFactory traceFactory)
        {
            string id = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var logger = new TriggeredJobRunLogger(triggeredJob.Name, id, environment, fileSystem, traceFactory);
            var triggeredJobStatus = new TriggeredJobStatus()
            {
                Status = "Initializing",
                StartTime = DateTime.UtcNow
            };
            logger.ReportStatus(triggeredJobStatus);
            return logger;
        }

        public void ReportEndRun()
        {
            var triggeredJobStatus = ReadJobStatusFromFile<TriggeredJobStatus>(TraceFactory, FileSystem, GetStatusFilePath()) ?? new TriggeredJobStatus();
            triggeredJobStatus.EndTime = DateTime.UtcNow;
            ReportStatus(triggeredJobStatus);
        }

        public void ReportStatus(string status)
        {
            var triggeredJobStatus = ReadJobStatusFromFile<TriggeredJobStatus>(TraceFactory, FileSystem, GetStatusFilePath()) ?? new TriggeredJobStatus();
            triggeredJobStatus.Status = status;
            ReportStatus(triggeredJobStatus);
        }

        protected override string HistoryPath
        {
            get { return _historyPath; }
        }

        public override void LogError(string error)
        {
            var triggeredJobStatus = ReadJobStatusFromFile<TriggeredJobStatus>(TraceFactory, FileSystem, GetStatusFilePath()) ?? new TriggeredJobStatus();
            triggeredJobStatus.Status = "Failed";
            ReportStatus(triggeredJobStatus);
            Log(Level.Err, error, isSystem: true);
        }

        public override void LogWarning(string warning)
        {
            Log(Level.Warn, warning, isSystem: true);
        }

        public override void LogInformation(string message)
        {
            Log(Level.Info, message, isSystem: true);
        }

        public override bool LogStandardOutput(string message)
        {
            Log(Level.Info, message);
            return true;
        }

        public override bool LogStandardError(string message)
        {
            Log(Level.Err, message);
            return true;
        }

        private void Log(Level level, string message, bool isSystem = false)
        {
            if (isSystem)
            {
                message = GetSystemFormattedMessage(level, message);
            }
            else
            {
                message = "[{0}] {1}\r\n".FormatInvariant(DateTime.UtcNow, message);
            }

            string logPath = level == Level.Err ? _errorFilePath : _outputFilePath;

            SafeLogToFile(logPath, message);
        }
    }
}