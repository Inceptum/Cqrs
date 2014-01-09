using NEventStore;
using NEventStore.Dispatcher;
using NLog;

namespace Inceptum.Cqrs
{
    class CommitDispatcher : IDispatchCommits
    {
        private readonly IEventPublisher m_EventPublisher;
        private readonly Logger m_Logger = LogManager.GetCurrentClassLogger();
        public CommitDispatcher(IEventPublisher eventPublisher)
        {
            m_EventPublisher = eventPublisher;
        }

        public void Dispose()
        {
        }

        public void Dispatch(Commit commit)
        {
            if (m_Logger.IsDebugEnabled) m_Logger.Debug(string.Format("Stream [{0}] Commit [{1}] dispatching {2} events", commit.StreamId, commit.CommitId, commit.Events.Count));
            foreach (var @event in commit.Events)
            {
                m_EventPublisher.PublishEvent(@event.Body);
                if (m_Logger.IsDebugEnabled) m_Logger.Debug(string.Format("Stream [{0}] Commit [{1}] event '{2}' dispatched", commit.StreamId, commit.CommitId, @event.Body.GetType()));
            }
        }
    }
}