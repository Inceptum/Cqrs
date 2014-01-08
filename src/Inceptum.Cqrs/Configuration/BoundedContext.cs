using System;
using System.Collections.Generic;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs.Configuration
{
    public class BoundedContext:IDisposable
    {
        internal Dictionary<Type, string> EventRoutes { get; set; }
        internal Dictionary<string, IEnumerable<Type>> EventsSubscriptions { get; set; }
        internal List<CommandSubscription> CommandsSubscriptions { get; set; }
        internal Dictionary<Tuple<Type, CommandPriority>, string> CommandRoutes { get; set; }
        internal EventsPublisher EventsPublisher { get; private set; }
        internal CommandDispatcher CommandDispatcher { get; private set; }
        internal EventDispatcher EventDispatcher { get; private set; }
        internal List<IProcess> Processes { get; private set; }
        internal IEventStoreAdapter EventStore { get; set; }
        readonly Dictionary<string,Destination> m_TempDestinations=new Dictionary<string, Destination>(); 
        public string Name { get; set; }
        public int ThreadCount { get; set; }
        public long FailedCommandRetryDelay { get; set; }
        public bool IsLocal { get; private  set; }
        public string LocalBoundedContext { get; private set; }

        internal BoundedContext(CqrsEngine cqrsEngine, string name, int threadCount, long failedCommandRetryDelay, bool isLocal, string localBoundedContext)
        {
            ThreadCount = threadCount;
            FailedCommandRetryDelay = failedCommandRetryDelay;
            IsLocal = isLocal;
            LocalBoundedContext = localBoundedContext;
            Name = name;
            EventsPublisher = new EventsPublisher(cqrsEngine, this);
            CommandDispatcher = new CommandDispatcher(Name, threadCount, failedCommandRetryDelay);
            EventDispatcher = new EventDispatcher(Name);
            Processes = new List<IProcess>();
        }

        public bool GetTempDestination(string transportId,Func<Destination> generate , out Destination destination)
        {
            lock (m_TempDestinations)
            {
                if (!m_TempDestinations.TryGetValue(transportId, out destination))
                {
                    destination = generate();
                    m_TempDestinations[transportId] = destination;
                    return true;
                }
            }
            return false;
        }

        public void Dispose()
        {
            CommandDispatcher.Dispose();
        }
    }
}