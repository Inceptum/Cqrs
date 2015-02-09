using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using NEventStore;
using NEventStore.Dispatcher;
using NEventStore.Logging;
using NEventStore.Persistence;

namespace Inceptum.Cqrs.EventSourcing
{
    public class DispatchScheduler : IScheduleDispatches
    {
        private static readonly ILog m_Logger = LogFactory.BuildLogger(typeof (SynchronousDispatchScheduler));

        private readonly IDispatchCommits m_Dispatcher;
        private readonly IPersistStreams m_Persistence;
        private const int BOUNDED_CAPACITY = 1024;
        private readonly BlockingCollection<Commit> m_Queue;
        private Task m_Worker;
        private bool m_Disposed;

        public DispatchScheduler(IDispatchCommits dispatcher, IPersistStreams persistence)
        {
            m_Dispatcher = dispatcher;
            m_Persistence = persistence;
            m_Queue = new BlockingCollection<Commit>(new ConcurrentQueue<Commit>(), BOUNDED_CAPACITY);
            
        }

        public void ScheduleDispatch(Commit commit)
        {
            m_Queue.Add(commit);
        }

        private void working()
        {
            foreach (var commit in m_Queue.GetConsumingEnumerable())
            {
                dispatchImmediately(commit);
                markAsDispatched(commit);
            }
        }

        private void dispatchImmediately(Commit commit)
        {
            try
            {
                m_Dispatcher.Dispatch(commit);
            }
            catch
            {
                m_Logger.Error("Unable To Dispatch commit {1} with dispatcher {0}", m_Dispatcher.GetType(), commit.CommitId);
                throw;
            }
        }

        private void markAsDispatched(Commit commit)
        {
            try
            {
                m_Persistence.MarkCommitAsDispatched(commit);
            }
            catch (ObjectDisposedException)
            {
                m_Logger.Warn("Unable To Mark Dispatched commit {0}", commit.CommitId);
            }
        }

        protected void Dispose(bool disposing)
        {
            if (!disposing || m_Disposed)
            {
                return;
            }
            m_Disposed = true;
            m_Queue.CompleteAdding();
            if (m_Worker != null)
            {
                m_Worker.Wait(TimeSpan.FromSeconds(30));
            }
            m_Dispatcher.Dispose();
            m_Persistence.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Initialize()
        {
            m_Persistence.Initialize();

            foreach (var commit in m_Persistence.GetUndispatchedCommits())
            {
                ScheduleDispatch(commit);
            }

            m_Worker = new Task(working);
            m_Worker.Start();
        }
    }
}