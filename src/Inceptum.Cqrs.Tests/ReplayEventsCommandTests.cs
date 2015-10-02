using System;
using System.IO;
using System.Threading;
using Inceptum.Cqrs.Configuration;
using Inceptum.Cqrs.InfrastructureCommands;
using NUnit.Framework;
using Rhino.Mocks;

namespace Inceptum.Cqrs.Tests
{
    class ReplayedEventsListener
    {
        public   int cnt=0;
        void Handle(int e)
        {
            Interlocked.Increment(ref cnt);
        }
        
    }
    public class ReplayingProcess : IProcess
    {
        public void Dispose()
        {
            
        }

        public void Start(ICommandSender commandSender, IEventPublisher eventPublisher)
        {
            commandSender.ReplayEvents("remote",DateTime.MinValue, null, typeof(int));
        }
    }

    [TestFixture]
    internal class ReplayEventsCommandTests
    {
        [Test]
        public void SerializeDeserializeTest()
        {
            using (var ms = new MemoryStream())
            {
                var replayEventsCommand = new ReplayEventsCommand { Destination = "12312", From = new DateTime(2013, 12, 31, 23, 59, 59), SerializationFormat = "protobuf", Types = new Type[] { typeof(string), typeof(int), typeof(ReplayEventsCommandTests) } };
                ProtoBuf.Serializer.Serialize(ms, replayEventsCommand);
                ms.Seek(0, SeekOrigin.Begin);
                var actual = ProtoBuf.Serializer.Deserialize<ReplayEventsCommand>(ms);

                Assert.AreEqual(replayEventsCommand.Destination, actual.Destination);
                Assert.AreEqual(replayEventsCommand.From, actual.From);
                Assert.AreEqual(replayEventsCommand.SerializationFormat, actual.SerializationFormat);
                CollectionAssert.AreEquivalent(replayEventsCommand.Types, actual.Types);
            }
        } 
        
        
        [Test]
        public void ReplayWithinProcessTest()
        {
            var process = new ReplayingProcess();
            var listener = new ReplayedEventsListener();
            var es=MockRepository.GenerateMock<IEventStoreAdapter>();
            es.Expect(x => x.GetEventsFrom(DateTime.MinValue, null, typeof (int))).Return(new object[] {10});
            using (new InMemoryCqrsEngine(


                     Register.BoundedContext("remote")
                                .ListeningInfrastructureCommands().On("inf")
                                .PublishingEvents(typeof(int)).With("events")
                                .WithEventStore(es),

                                             
                     Register.BoundedContext("local")
                                .PublishingCommands(typeof(ReplayEventsCommand)).To("remote").With("inf")
                                .ListeningEvents(typeof(int)).From("remote").On("events")
                                .WithProcess(process)
                                .WithProjection(listener,"remote")

                    
             ))
            {
                Thread.Sleep(1000);
                Console.WriteLine("Disposing...");
            }
            Assert.That(listener.cnt,Is.GreaterThan(0));
        }
    }
}