using Penguin.Cms.Logging.Entities;
using Penguin.Errors;
using Penguin.Logging;
using Penguin.Messaging.Abstractions.Interfaces;
using Penguin.Messaging.Core;
using Penguin.Messaging.Logging.Extensions;
using Penguin.Persistence.Abstractions.Interfaces;
using Penguin.Reflection;
using Penguin.Workers.Abstractions;
using Penguin.Workers.Repositories;
using System;
using System.ComponentModel;
using System.Runtime.ExceptionServices;

namespace Penguin.Cms.Workers
{
    /// <summary>
    /// Base class for all workers that assume existence of core CMS functionality
    /// </summary>
    public abstract class CmsWorker : Worker
    {
        /// <summary>
        /// Creates a new instance of the CmsWorker class
        /// </summary>
        /// <param name="workerRepository">A worker repository to store event data in</param>
        /// <param name="logEntryRepository">A LogEntry repository implementation to use when building the underlying logger</param>
        /// <param name="errorRepository">An Error repository implementation for handling the creation of worker errors</param>
        /// <param name="messageBus">An optional message bus to use for the worker and logger</param>
        public CmsWorker(WorkerRepository workerRepository, IRepository<LogEntry> logEntryRepository, IRepository<AuditableError> errorRepository, MessageBus messageBus = null)
        {
            this.MessageBus = messageBus;

            this.Logger = new Logger(this, messageBus, logEntryRepository, errorRepository);

            this.WorkerRepository = workerRepository;

            MessageBus.SubscribeAll(TypeFactory.GetAllImplementations(typeof(IMessageHandler)));

            this.UpdateLastRun();
        }

        /// <summary>
        /// Attempts to get the last-run time from the worker repository
        /// </summary>
        public override void UpdateLastRun() => this.LastRun = this.WorkerRepository.GetLastRun(this.GetType());

        /// <summary>
        /// Attempts to start the worker and serves as a wrapper for all run attempts
        /// </summary>
        /// <param name="sender">Not used</param>
        /// <param name="e">Not used</param>
        public override void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            using (this.WorkerRepository.WriteContext())
            {
                try
                {
                    this.IsBusy = true;
                    this.Logger.LogInfo("Starting worker {0}", this.TypeName);
                    base.Worker_DoWork(sender, e);
                    this.Logger.LogInfo("Ending worker {0}", this.TypeName);
                    this.WorkerRepository.SetLastRun(this.GetType());
                }
                catch (Exception ex)
                {
                    MessageBus?.Log(ex);
                    this.Logger.LogException(ex);
                    throw;
                }

                this.Logger.Dispose();
                this.IsBusy = false;
            }
        }

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

        /// <summary>
        /// Logs an informational message during the worker run
        /// </summary>
        /// <param name="toLog"></param>
        protected void LogInfo(string toLog) => this.Logger.LogInfo(toLog, this.TypeName);

        private string _typeName { get; set; }

        private string TypeName { get { if (this._typeName is null) { this._typeName = this.GetType().Name; } return this._typeName; } }
    }
}