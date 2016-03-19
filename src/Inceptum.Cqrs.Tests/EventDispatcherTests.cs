using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Castle.Components.DictionaryAdapter;
using Inceptum.Cqrs.Configuration;
using Inceptum.Messaging.Contract;
using NUnit.Framework;

namespace Inceptum.Cqrs.Tests
{
    class EventHandler 
    {
        public EventHandler(bool fail = false)
        {
            m_Fail = fail;
        }

        public readonly List<object> HandledEvents=new List<object>();
        private  bool m_Fail;

        public bool Fail
        {
            get { return m_Fail; }
            set { m_Fail = value; }
        }

        public bool FailOnce { get; set; }

        public void Handle(string e)
        {
            HandledEvents.Add(e);
            if (m_Fail || FailOnce)
            {
                FailOnce = false;
                throw new Exception();
            }
        }        
        
        
    /*    public void Handle(int e)
        {
            HandledEvents.Add(e);
            if (m_Fail)
                throw new Exception();
        }       */    


        public CommandHandlingResult[] Handle(int[] e)
        {
            return e.Select(i =>
            {
                HandledEvents.Add(i);
                return new CommandHandlingResult {Retry = m_Fail, RetryDelay = 600};
            }).ToArray();

        }    
        public CommandHandlingResult Handle(Exception e)
        {
            HandledEvents.Add(e);
            return new CommandHandlingResult(){Retry = true,RetryDelay = 100};
        }

        public void OnBatchStart()
        {
            BatchStartReported = true;
        }

        public bool BatchStartReported { get; set; }
        public bool BatchFinishReported { get; set; }


        public void OnBatchFinish()
        {
            BatchFinishReported = true;
        }
    }   
    

    [TestFixture]
    public class EventDispatcherTests
    {
       
        [Test]
        public void WireTest()
        {
            var dispatcher = new EventDispatcher("testBC");
            var handler = new EventHandler();
            dispatcher.Wire("testBC",handler);
            dispatcher.Dispatch("testBC", "test", (delay, acknowledge) => { });
            dispatcher.Dispatch("testBC", 1, (delay, acknowledge) => { });
            Assert.That(handler.HandledEvents, Is.EquivalentTo(new object[] { "test" ,1}), "Some events were not dispatched");
        }

        [Test]
        public void MultipleHandlersDispatchTest()
        {
            var dispatcher=new EventDispatcher("testBC");
            var handler1 = new EventHandler();
            var handler2 = new EventHandler();
            dispatcher.Wire("testBC",handler1);
            dispatcher.Wire("testBC",handler2);
            dispatcher.Dispatch("testBC","test", (delay, acknowledge) => { });
            Assert.That(handler1.HandledEvents, Is.EquivalentTo(new[] { "test" }), "Event was not dispatched");
            Assert.That(handler2.HandledEvents, Is.EquivalentTo(new[] { "test" }), "Event was not dispatched");
        }


        [Test]
        public void FailingHandlersDispatchTest()
        {
            var dispatcher=new EventDispatcher("testBC");
            var handler1 = new EventHandler();
            var handler2 = new EventHandler(true);
            dispatcher.Wire("testBC",handler1);
            dispatcher.Wire("testBC", handler2);
            Tuple<long, bool> result=null;
            dispatcher.Dispatch("testBC", "test", (delay, acknowledge) => { result = Tuple.Create(delay, acknowledge); });
            Assert.That(handler1.HandledEvents, Is.EquivalentTo(new[] { "test" }), "Event was not dispatched");
            Assert.That(result,Is.Not.Null,"fail was not reported");
            Assert.That(result.Item2,Is.False,"fail was not reported");
            Assert.That(result.Item1, Is.EqualTo(EventDispatcher.m_FailedEventRetryDelay), "fail was not reported");
        }

        [Test]
        public void RetryingHandlersDispatchTest()
        {
            var dispatcher=new EventDispatcher("testBC");
            var handler = new EventHandler();
            dispatcher.Wire("testBC", handler);
            Tuple<long, bool> result=null;
            dispatcher.Dispatch("testBC", new Exception(), (delay, acknowledge) => { result = Tuple.Create(delay, acknowledge); });
            Assert.That(result,Is.Not.Null,"fail was not reported");
            Assert.That(result.Item2,Is.False,"fail was not reported");
            Assert.That(result.Item1, Is.EqualTo(100), "fail was not reported");
        }


        [Test]
        public void BatchDispatchTest()
        {
            var dispatcher = new EventDispatcher("testBC");
            var handler = new EventHandler();
            dispatcher.Wire("testBC", handler);
            Tuple<long, bool> result = null;
            handler.FailOnce = true;
            dispatcher.Dispatch("testBC",new []
            {
                Tuple.Create<object,AcknowledgeDelegate>("a", (delay, acknowledge) => {result = Tuple.Create(delay, acknowledge);  }),
                Tuple.Create<object,AcknowledgeDelegate>("b", (delay, acknowledge) => { }),
                Tuple.Create<object,AcknowledgeDelegate>("с", (delay, acknowledge) => { })
            });

            Assert.That(result, Is.Not.Null, "fail was not reported");
            Assert.That(result.Item2, Is.False, "fail was not reported");
            Assert.That(handler.HandledEvents.Count, Is.EqualTo(3), "not all events were handled (exception in first event handling prevented following events processing?)");
        }

        [Test]
        public void BatchDispatchTriggeringBySizeTest()
        {
            var dispatcher = new EventDispatcher("testBC");
            var handler = new EventHandler();
            dispatcher.Wire("testBC", handler, 3, 1000000, h => ((EventHandler)h).OnBatchStart(), h => ((EventHandler)h).OnBatchFinish());
            Tuple<long, bool> result = null;
            handler.FailOnce = false;
            dispatcher.Dispatch("testBC", new[]
            {
                Tuple.Create<object,AcknowledgeDelegate>("a", (delay, acknowledge) => {result = Tuple.Create(delay, acknowledge);  }),
                Tuple.Create<object,AcknowledgeDelegate>("b", (delay, acknowledge) => { }),
            });
            Assert.That(handler.HandledEvents.Count, Is.EqualTo(0), "Events were delivered before batch is filled");
            dispatcher.Dispatch("testBC", new[]
            {
                Tuple.Create<object,AcknowledgeDelegate>("с", (delay, acknowledge) => { })
            });
            Assert.That(handler.HandledEvents.Count, Is.Not.EqualTo(0), "Events were not delivered after batch is filled");
            Assert.That(handler.HandledEvents.Count, Is.EqualTo(3), "Not all events were delivered");
            Assert.That(handler.BatchStartReported, Is.True, "Batch start callback was not called");
            Assert.That(handler.BatchFinishReported, Is.True, "Batch after apply  callback was not called");
        }

        [Test]
        public void BatchDispatchTriggeringByTimeoutTest()
        {
            var dispatcher = new EventDispatcher("testBC");
            var handler = new EventHandler();
            dispatcher.Wire("testBC", handler, 3, 1, h => ((EventHandler)h).OnBatchStart(), h =>((EventHandler)h).OnBatchFinish());
            Tuple<long, bool> result = null;
            handler.FailOnce = false;
            dispatcher.Dispatch("testBC", new[]
            {
                Tuple.Create<object,AcknowledgeDelegate>("a", (delay, acknowledge) => {result = Tuple.Create(delay, acknowledge);  }),
                Tuple.Create<object,AcknowledgeDelegate>("b", (delay, acknowledge) => { })
            });
            Assert.That(handler.HandledEvents.Count, Is.EqualTo(0), "Events were delivered before batch apply timeoout");
            Thread.Sleep(2000);
            Assert.That(handler.HandledEvents.Count, Is.Not.EqualTo(0), "Events were not delivered after batch is filled");
            Assert.That(handler.HandledEvents.Count, Is.EqualTo(2), "Not all events were delivered");
            Assert.That(handler.BatchStartReported, Is.True, "Batch start callback was not called");
            Assert.That(handler.BatchFinishReported, Is.True, "Batch after apply  callback was not called");
        }
    }
}
