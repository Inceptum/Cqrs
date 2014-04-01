using System;
using System.IO;
using Inceptum.Cqrs.InfrastructureCommands;
using NUnit.Framework;

namespace Inceptum.Cqrs.Tests
{
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
    }
}