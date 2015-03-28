using System;
using System.Collections.Generic;
using System.Threading;
using Inceptum.Cqrs.InfrastructureCommands;
using Inceptum.Messaging.Contract;
using NLog;

namespace Inceptum.Cqrs
{
    class Replay
    {
        private readonly Action<long> m_Callback;
        private long m_Counter;
        public Guid Id { get; set; }
        public int BatchSize { get; private set; }
        List<Tuple<object, AcknowledgeDelegate>> m_Events=new List<Tuple<object, AcknowledgeDelegate>>(); 
        public long Counter
        {
            get { return m_Counter; }
            set { m_Counter = value; }
        }

        public ReplayFinishedEvent FinishedEvent { get; set; }
        public AcknowledgeDelegate FinishedEventAcknowledge { get; set; }

        public Replay(Guid id, Action<long> callback, int batchSize)
        {
            m_Callback = callback;
            Id = id;
            BatchSize = batchSize;
        }

        public long Increment()
        {
            return Interlocked.Increment(ref m_Counter);
        }


        internal bool ReportReplayFinishedIfRequired(Logger logger)
        {
            if (FinishedEvent == null || FinishedEvent.EventsCount != Counter)
                return false;

            if (FinishedEventAcknowledge == null)
                return true;

            try
            {
                m_Callback(Counter);
                FinishedEventAcknowledge(0, true);
                return true;
            }
            catch (Exception e)
            {
                logger.WarnException("Failed to finish replay due to callback failure", e);
                FinishedEventAcknowledge(60000, false);
                return false;
            }
        }

        public IEnumerable<Tuple<object, AcknowledgeDelegate>> GetEventsToDispatch(object @event, AcknowledgeDelegate acknowledge)
        {
            var replayFinishedEvent = @event as ReplayFinishedEvent;
            if (replayFinishedEvent!=null)
            {
                 FinishedEvent = (ReplayFinishedEvent) @event ;
                 FinishedEventAcknowledge = acknowledge;
                if(BatchSize <= 1)
                    return new Tuple<object, AcknowledgeDelegate>[0];
            }
            
            var tuple = Tuple.Create(@event, acknowledge);
            if (BatchSize <= 1)
            {
                return new[] {tuple};
            }

            if (replayFinishedEvent== null)
                m_Events.Add(tuple);

            if (m_Events.Count >= BatchSize || FinishedEvent!=null)
            {
                var r = m_Events.ToArray();
                m_Events.Clear();
                return r;
            }
            return new Tuple<object, AcknowledgeDelegate>[0];
        }
    }
}