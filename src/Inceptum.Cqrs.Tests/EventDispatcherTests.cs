using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Inceptum.Cqrs.Configuration;
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
        private readonly bool m_Fail;

        public void Handle(string e)
        {
            HandledEvents.Add(e);
            if (m_Fail)
                throw new Exception();
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
            /*var dispatcher = new EventDispatcher("testBC");
            var handler1 = new EventHandler();
            dispatcher.Wire("testBC", handler1);
            dispatcher.BatchDispatch("testBC", Enumerable.Range(1,10).Select(i=>(object)i), (delay, acknowledge) => { });
            Assert.That(handler1.HandledEvents, Is.EquivalentTo(Enumerable.Range(1, 10)), "Events were not dispatched");
            */
        }
    }
}
