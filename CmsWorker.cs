﻿using Loxifi;
using Penguin.Cms.Errors;
using Penguin.Cms.Logging;
using Penguin.Cms.Logging.Services;
using Penguin.Messaging.Abstractions.Interfaces;
using Penguin.Messaging.Core;
using Penguin.Messaging.Logging.Extensions;
using Penguin.Persistence.Abstractions.Interfaces;
using Penguin.Reflection;
using Penguin.Workers.Abstractions;
using Penguin.Workers.Repositories;
using System;
using System.ComponentModel;

namespace Penguin.Cms.Workers
{
    /// <summary>
    /// Base class for all workers that assume existence of core CMS functionality
    /// </summary>
    public abstract class CmsWorker : Worker
    {
        /// <summary>
        /// The logger to use for recording worker activity
        /// </summary>
        protected Logger Logger { get; set; }

        /// <summary>
        /// The optional message bus to send logged information over
        /// </summary>
        protected MessageBus MessageBus { get; set; }

        /// <summary>
        /// A worker repository to store event data in
        /// </summary>
        protected WorkerRepository WorkerRepository { get; set; }

        private string _typeName { get; set; }

        private string TypeName
        { get { _typeName ??= GetType().Name; return _typeName; } }

        /// <summary>
        /// Creates a new instance of the CmsWorker class
        /// </summary>
        /// <param name="workerRepository">A worker repository to store event data in</param>
        /// <param name="logEntryRepository">A LogEntry repository implementation to use when building the underlying logger</param>
        /// <param name="errorRepository">An Error repository implementation for handling the creation of worker errors</param>
        /// <param name="messageBus">An optional message bus to use for the worker and logger</param>
        [Obsolete]
        protected CmsWorker(WorkerRepository workerRepository, IRepository<LogEntry> logEntryRepository, IRepository<AuditableError> errorRepository, MessageBus messageBus = null)
        {
            MessageBus = messageBus;

            Logger = new Logger(this, messageBus, logEntryRepository, errorRepository);

            WorkerRepository = workerRepository;

            MessageBus.SubscribeAll(TypeFactory.Default.GetAllImplementations(typeof(IMessageHandler)));

            UpdateLastRun();
        }

        /// <summary>
        /// Attempts to get the last-run time from the worker repository
        /// </summary>
        public override void UpdateLastRun()
        {
            LastRun = WorkerRepository.GetLastRun(GetType());
        }

        /// <summary>
        /// Attempts to start the worker and serves as a wrapper for all run attempts
        /// </summary>
        /// <param name="sender">Not used</param>
        /// <param name="e">Not used</param>
        public override void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            using (WorkerRepository.WriteContext())
            {
                try
                {
                    IsBusy = true;
                    Logger.LogInfo("Starting worker {0}", TypeName);
                    base.Worker_DoWork(sender, e);
                    Logger.LogInfo("Ending worker {0}", TypeName);
                    WorkerRepository.SetLastRun(GetType());
                }
                catch (Exception ex)
                {
                    MessageBus?.Log(ex);
                    Logger.LogException(ex);
                    throw;
                }

                Logger.Dispose();
                IsBusy = false;
            }
        }

        /// <summary>
        /// Logs an informational message during the worker run
        /// </summary>
        /// <param name="toLog"></param>
        protected void LogInfo(string toLog)
        {
            Logger.LogInfo(toLog, TypeName);
        }
    }
}