using System;
using System.Collections.Generic;
using System.Linq;
using Inceptum.Cqrs.NEventStore;
using NEventStore;
using NEventStore.Conversion;
using NEventStore.Dispatcher;
using NEventStore.Logging;
using NUnit.Framework;
using Rhino.Mocks;

namespace Inceptum.Cqrs.Tests
{
    class TestAggregateRootNameChangedEventUpgrade : IUpgradeEvents<TestAggregateRootNameChangedEvent>, IUpconvertEvents<TestAggregateRootNameChangedEvent, TestAggregateRootCreatedEvent>
    {
        public IEnumerable<object> Upgrade(TestAggregateRootNameChangedEvent sourceEvent)
        {
            if (sourceEvent.Name == "original")
            {
                yield return new TestAggregateRootNameChangedEvent { Name = "new1" };
                yield return new TestAggregateRootNameChangedEvent { Name = "new2" };
                yield break;
            }

            yield return sourceEvent;
        }

        public TestAggregateRootCreatedEvent Convert(TestAggregateRootNameChangedEvent sourceEvent)
        {
            return new TestAggregateRootCreatedEvent() { Id = sourceEvent.Id };
        }
    }
    [TestFixture]
    public class EventUpgradeWireupTests
    {
        [Test]
        public void EventUpgradeWireupTest()
        {
            var log = MockRepository.GenerateMock<ILog>();
            var dispatchCommits = MockRepository.GenerateMock<IDispatchCommits>();
            var es = Wireup.Init()
                .LogTo(type => log)
                .UsingInMemoryPersistence()
                .InitializeStorageEngine()
                .UsingJsonSerialization()
                .UsingSynchronousDispatchScheduler()
                .DispatchTo(dispatchCommits)
                .UsingEventUpgrading().AddUpgrade(new TestAggregateRootNameChangedEventUpgrade())
                .Build();

            var streamId = Guid.NewGuid();
            var stream = es.CreateStream(streamId);
            stream.Add(new EventMessage() { Body = new TestAggregateRootNameChangedEvent() { Name = "original" } });
            var commitId = Guid.Parse("D552E345-81CB-40D8-AAB7-6BAD7E6B407B");
            stream.CommitChanges(commitId);
            stream = es.OpenStream(streamId, 0, Int32.MaxValue);
            Assert.That(stream.CommittedEvents.Count, Is.EqualTo(2));
            Assert.That(stream.CommittedEvents.Select(e => (e.Body as TestAggregateRootNameChangedEvent).Name), Is.EquivalentTo(new[] { "new1", "new2" }));
        }

        [Test]
        public void EventUpconvertWireupTest()
        {
            var log = MockRepository.GenerateMock<ILog>();
            var dispatchCommits = MockRepository.GenerateMock<IDispatchCommits>();
            var es = Wireup.Init()
                .LogTo(type => log)
                .UsingInMemoryPersistence()
                .InitializeStorageEngine()
                .UsingJsonSerialization()
                .UsingSynchronousDispatchScheduler()
                .DispatchTo(dispatchCommits)
                .UsingEventUpconversion().AddConverter(new TestAggregateRootNameChangedEventUpgrade())
                .Build();

            var streamId = Guid.NewGuid();
            var stream = es.CreateStream(streamId);
            stream.Add(new EventMessage() { Body = new TestAggregateRootNameChangedEvent() { Name = "original" } });
            var commitId = Guid.Parse("D552E345-81CB-40D8-AAB7-6BAD7E6B407B");
            stream.CommitChanges(commitId);
            stream = es.OpenStream(streamId, 0, Int32.MaxValue);
            Assert.That(stream.CommittedEvents.Count, Is.EqualTo(1));
            Assert.IsTrue(stream.CommittedEvents.First().Body is TestAggregateRootCreatedEvent);
        }

    }
}