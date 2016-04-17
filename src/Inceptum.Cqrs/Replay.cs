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
        private long m_ExpectedCount=-1;
        public Guid Id { get; private set; }

        public long Counter
        {
            get { return m_Counter; }
            set { m_Counter = value; }
        }

        private AcknowledgeDelegate FinishedEventAcknowledge { get; set; }

        public Replay(Guid id, Action<long> callback)
        {
            m_Callback = callback;
            Id = id;
        }
 

        internal bool ReportReplayFinishedIfRequired(Logger logger,bool incrementCounter , AcknowledgeDelegate finishedEventAcknowledge=null, ReplayFinishedEvent finishedEvent = null)
        {
            if (finishedEvent != null)
                m_ExpectedCount = finishedEvent.EventsCount;
            if (incrementCounter)
                Interlocked.Increment(ref m_Counter);

            FinishedEventAcknowledge = finishedEventAcknowledge ?? FinishedEventAcknowledge;
            if (FinishedEventAcknowledge == null)
                return false;

            try
            {
                if (m_ExpectedCount != Counter)
                    return false;
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
    }
}