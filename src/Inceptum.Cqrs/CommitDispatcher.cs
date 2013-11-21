using NEventStore;
using NEventStore.Dispatcher;

namespace Inceptum.Cqrs
{
    class CommitDispatcher : IDispatchCommits
    {
        private readonly IEventPublisher m_EventPublisher;

        public CommitDispatcher(IEventPublisher eventPublisher)
        {
            m_EventPublisher = eventPublisher;
        }

        public void Dispose()
        {
        }

        public void Dispatch(Commit commit)
        {
            foreach (var @event in commit.Events)
            {
                m_EventPublisher.PublishEvent(@event.Body);
            }
        }
    }
}