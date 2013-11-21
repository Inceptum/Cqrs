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
        
        
        public void Handle(int e)
        {
            HandledEvents.Add(e);
            if (m_Fail)
                throw new Exception();
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
            dispatcher.Wire(handler);
            dispatcher.Dispacth("test", (delay, acknowledge) => { });
            dispatcher.Dispacth(1, (delay, acknowledge) => { });
            Assert.That(handler.HandledEvents, Is.EquivalentTo(new object[] { "test" ,1}), "Some events were not dispatched");
        }

        [Test]
        public void MultipleHandlersDispatchTest()
        {
            var dispatcher=new EventDispatcher("testBC");
            var handler1 = new EventHandler();
            var handler2 = new EventHandler();
            dispatcher.Wire(handler1);
            dispatcher.Wire(handler2);
            dispatcher.Dispacth("test", (delay, acknowledge) => { });
            Assert.That(handler1.HandledEvents, Is.EquivalentTo(new[] { "test" }), "Event was not dispatched");
            Assert.That(handler2.HandledEvents, Is.EquivalentTo(new[] { "test" }), "Event was not dispatched");
        }


        [Test]
        public void FailingHandlersDispatchTest()
        {
            var dispatcher=new EventDispatcher("testBC");
            var handler1 = new EventHandler();
            var handler2 = new EventHandler(true);
            dispatcher.Wire(handler1);
            dispatcher.Wire(handler2);
            Tuple<long, bool> result=null;
            dispatcher.Dispacth("test", (delay, acknowledge) => { result = Tuple.Create(delay, acknowledge); });
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
            dispatcher.Wire(handler);
            Tuple<long, bool> result=null;
            dispatcher.Dispacth(new Exception(), (delay, acknowledge) => { result = Tuple.Create(delay, acknowledge); });
            Assert.That(result,Is.Not.Null,"fail was not reported");
            Assert.That(result.Item2,Is.False,"fail was not reported");
            Assert.That(result.Item1, Is.EqualTo(100), "fail was not reported");
        }
    }
}
