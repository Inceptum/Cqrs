using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using Castle.Core.Logging;
using CommonDomain;
using CommonDomain.Core;
using CommonDomain.Persistence;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using Inceptum.Cqrs.Configuration;
using Inceptum.Messaging;
using Inceptum.Messaging.Configuration;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.RabbitMq;
using Inceptum.Messaging.Serialization;
using NEventStore;
using NEventStore.Logging;
using NUnit.Framework;
using Rhino.Mocks;

namespace Inceptum.Cqrs.Tests
{
    public class  TestAggregateRootCreatedEvent
    {
        public Guid Id { get; set; }
    }

    public class TestAggregateRootNameChangedEvent
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
    }

    public class TestAggregateRoot : AggregateBase
    {
        private string m_Name;

        public string Name
        {
            get { return m_Name; }
            set { RaiseEvent(new TestAggregateRootNameChangedEvent {Id = Id, Name = value}); }
        }

        public TestAggregateRoot()
        {
        }

        public TestAggregateRoot(Guid id,IMemento memento=null)
             
        {
            Id = id;
        }

        public void Create()
        {
            RaiseEvent(new TestAggregateRootCreatedEvent{Id=Id});
        }

        protected void Apply(TestAggregateRootCreatedEvent e)
        {

        }

        protected void Apply(TestAggregateRootNameChangedEvent e)
        {
            m_Name= e.Name;
        }
    }

    class EsCommandHandler
    {

        public void Handle(string command, IEventPublisher eventPublisher, IRepository repository)
        {
            var guid = new Guid(command.Substring(0,36));
            command=command.Substring(37);
            if (command == "create")
            {
                var ar = new TestAggregateRoot(guid);
                ar.Create();
                repository.Save(ar, Guid.NewGuid());
            }
            if (command.StartsWith("changeName:"))
            {
                var ar = repository.GetById<TestAggregateRoot>(guid);
                ar.Name = command.Replace("changeName:","");
                repository.Save(ar, Guid.NewGuid());
            }
            Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " command recived:" + command);
        } 
    }

    class CommandHandler
    {
        public List<object> AcceptedCommands=new List<object>(); 
        private readonly ICommandSender m_Engine;
        private int counter = 0;
        private int m_ProcessingTimeout;

        public CommandHandler(int processingTimeout)
        {
            m_ProcessingTimeout = processingTimeout;
        }
        public CommandHandler():this(0)
        {
        }

        
        public void Handle(decimal command, IEventPublisher eventPublisher, IRepository repository)
        {
            Thread.Sleep(m_ProcessingTimeout);
            Console.WriteLine(Thread.CurrentThread.ManagedThreadId+" command recived:" + command);
            eventPublisher.PublishEvent(++counter);
            lock (AcceptedCommands)
                AcceptedCommands.Add(command);
        }
        public void Handle(string command,IEventPublisher eventPublisher)
        {
            Thread.Sleep(m_ProcessingTimeout);
            Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " command recived:" + command);
            eventPublisher.PublishEvent(++counter);
            lock (AcceptedCommands)
                AcceptedCommands.Add(command);
        }
        public void Handle(DateTime command, IEventPublisher eventPublisher)
        {
            Thread.Sleep(m_ProcessingTimeout);
            Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " command recived:" + command);
            eventPublisher.PublishEvent(++counter);
            lock (AcceptedCommands)
                AcceptedCommands.Add(command);
        }
    }

    class EventsListener
    {
        public List<object> Handled=new List<object>();
        public void Handle(TestAggregateRootNameChangedEvent  e,string boundedContext)
        {
            Handled.Add(e);
            Console.WriteLine(boundedContext + ":" + e);
        }
        public void Handle(TestAggregateRootCreatedEvent e, string boundedContext)
        {
            Handled.Add(e);
            Console.WriteLine(boundedContext + ":" + e);

        }

        public void Handle(int e,string boundedContext)
        {
            Handled.Add(e);

            Console.WriteLine(boundedContext+":"+e);
        }  
    }

    [TestFixture]
    public class CqrsEngineTests
    {

        [Test]
        [ExpectedException(typeof (ConfigurationErrorsException),
            ExpectedMessage =
                "Can not register System.String as command in bound context bc, it is already registered as event")]
        public void BoundedContextCanNotHaveEventAndCommandOfSameType()
        {
            new InMemoryCqrsEngine(LocalBoundedContext.Named("bc")
                                                      .PublishingEvents(typeof (string))
                                                      .To("eventExchange")
                                                      .RoutedTo("eventQueue")
                                                      .ListeningCommands(typeof (string))
                                                      .On("commandQueue")
                                                      .RoutedFrom("commandExchange"));
        }


        [Test]
        public void ListenSameCommandOnDifferentEndpointsTest()
        {
            using (
                var messagingEngine =
                    new MessagingEngine(
                        new TransportResolver(new Dictionary<string, TransportInfo>
                            {
                                {"InMemory", new TransportInfo("none", "none", "none", null, "InMemory")}
                            })))
            {
                var commandHandler = new CommandHandler();
                using (var engine = new CqrsEngine(messagingEngine,
                                                   new InMemoryEndpointResolver(),
                                                   LocalBoundedContext.Named("bc")
                                                                      .PublishingEvents(typeof (int)).To("eventExchange").RoutedTo("eventQueue")
                                                                      .ListeningCommands(typeof (string)).On("exchange1").On("exchange2").RoutedFrom("commandQueue")
                                                                      .WithCommandsHandler(commandHandler))
                    )
                {
                    messagingEngine.Send("test1", new Endpoint("InMemory", "bc.exchange1", serializationFormat: "json"));
                    messagingEngine.Send("test2", new Endpoint("InMemory", "bc.exchange2", serializationFormat: "json"));
                    messagingEngine.Send("test3", new Endpoint("InMemory", "bc.exchange3", serializationFormat: "json"));
                    Thread.Sleep(2000);
                    Assert.That(commandHandler.AcceptedCommands, Is.EquivalentTo(new[] { "test1", "test2" }));
                }
            }
        }



        [Test]
        [Ignore]
        public void AllThreadsAreStoppedAfterCqrsDisposeTest()
        {
            var initialThreadCount = Process.GetCurrentProcess().Threads.Count;
            
           
            Console.WriteLine(initialThreadCount);
            using (
                var messagingEngine =
                    new MessagingEngine(
                        new TransportResolver(new Dictionary<string, TransportInfo>
                            {
                                {"InMemory", new TransportInfo("none", "none", "none", null, "InMemory")}
                            })))
            {
                using (var engine = new CqrsEngine(messagingEngine,
                                                   new InMemoryEndpointResolver(),
                                                   LocalBoundedContext.Named("bc").ConcurrencyLevel(1)
                                                                      .PublishingEvents(typeof (int))
                                                                      .To("eventExchange")
                                                                      .RoutedTo("eventQueue")
                                                                      .ListeningCommands(typeof (string))
                                                                      .On("exchange1", CommandPriority.Low)
                                                                      .On("exchange2", CommandPriority.High)
                                                                      .RoutedFrom("commandQueue")

                                                                      .WithCommandsHandler(new CommandHandler(100)))
                    )
                {
                    Console.WriteLine(Process.GetCurrentProcess().Threads.Count);
                }
            }
            Assert.That(Process.GetCurrentProcess().Threads.Count, Is.EqualTo(initialThreadCount),
                        "Some threads were not stopped");
        }

        [Test]
        public void PrioritizedCommandsProcessingTest()
        {
            using (
                var messagingEngine =
                    new MessagingEngine(
                        new TransportResolver(new Dictionary<string, TransportInfo>
                            {
                                {"InMemory", new TransportInfo("none", "none", "none", null, "InMemory")}
                            })))
            {
                var commandHandler = new CommandHandler(100);
                using (var engine = new CqrsEngine(messagingEngine,
                                                   new InMemoryEndpointResolver(),
                                                   LocalBoundedContext.Named("bc").ConcurrencyLevel(1)
                                                                      .PublishingEvents(typeof (int)).To("eventExchange").RoutedTo("eventQueue")
                                                                      .ListeningCommands(typeof (string)).On("exchange1", CommandPriority.Low).On("exchange2", CommandPriority.High).RoutedFrom("commandQueue")
                                                                      .WithCommandsHandler(commandHandler))
                    )
                {
                    messagingEngine.Send("low1", new Endpoint("InMemory", "bc.exchange1", serializationFormat: "json"));
                    messagingEngine.Send("low2", new Endpoint("InMemory", "bc.exchange1", serializationFormat: "json"));
                    messagingEngine.Send("low3", new Endpoint("InMemory", "bc.exchange1", serializationFormat: "json"));
                    messagingEngine.Send("low4", new Endpoint("InMemory", "bc.exchange1", serializationFormat: "json"));
                    messagingEngine.Send("low5", new Endpoint("InMemory", "bc.exchange1", serializationFormat: "json"));
                    messagingEngine.Send("low6", new Endpoint("InMemory", "bc.exchange1", serializationFormat: "json"));
                    messagingEngine.Send("low7", new Endpoint("InMemory", "bc.exchange1", serializationFormat: "json"));
                    messagingEngine.Send("low8", new Endpoint("InMemory", "bc.exchange1", serializationFormat: "json"));
                    messagingEngine.Send("low9", new Endpoint("InMemory", "bc.exchange1", serializationFormat: "json"));
                    messagingEngine.Send("low10", new Endpoint("InMemory", "bc.exchange1", serializationFormat: "json"));
                    messagingEngine.Send("high", new Endpoint("InMemory", "bc.exchange2", serializationFormat: "json"));
                    Thread.Sleep(2000);
                    Console.WriteLine(string.Join("\n", commandHandler.AcceptedCommands));
                    Assert.That(commandHandler.AcceptedCommands.Take(2).Any(c => (string)c == "high"), Is.True);
                }
            }
        }



        [Test]
        [Ignore("investigation test")]
        public void CqrsEngineTest()
        {
            var serializationManager = new SerializationManager();
            serializationManager.RegisterSerializerFactory(new JsonSerializerFactory());
            var transportResolver =
                new TransportResolver(new Dictionary<string, TransportInfo>
                    {
                        {"test", new TransportInfo("localhost", "guest", "guest", null, "RabbitMq")}
                    });
            var messagingEngine = new MessagingEngine(transportResolver, new RabbitMqTransportFactory())
                {
                    Logger = new ConsoleLogger(LoggerLevel.Debug)
                };
            using (messagingEngine)
            {


                var cqrsEngine = new CqrsEngine(messagingEngine, new FakeEndpointResolver(),
                    LocalBoundedContext.Named("integration")
                        .PublishingEvents(typeof (int)).To("eventExchange").RoutedTo("eventQueue")
                        .ListeningCommands(typeof (string)).On("commandExchange").RoutedFrom("commandQueue")
                        .WithCommandsHandler<CommandsHandler>(),
                    LocalBoundedContext.Named("bc").WithProjection<EventListener>("integration")
                    );

/*                var cqrsEngine = new CqrsEngine(messagingEngine, new RabbitMqConventionEndpointResolver("test","json",new EndpointResolver(new Dictionary<string, Endpoint>())),
                                                LocalBoundedContext.Named("integration")
                                                                   .PublishingEvents(typeof (int)).To("events").RoutedToSameEndpoint()
                                                                   .ListeningCommands(typeof (string)).On("commands").RoutedFromSameEndpoint()
                                                                   .WithCommandsHandler<CommandsHandler>(),
                                                LocalBoundedContext.Named("bc").WithProjection<EventListener>("integration")
                    );*/
                /* var c=new commandSender(messagingEngine, RemoteBoundedContext.Named("integration")
                                                    .ListeningCommands(typeof(TestCommand)).On(new Endpoint())
                                                    .PublishingEvents(typeof(TransferCreatedEvent)).To(new Endpoint()),
                                                    LocalBoundedContext.Named("testBC")
                                                    .ListeningCommands(typeof(TestCommand)).On(new Endpoint("test", "unistream.u1.commands", true))
                                                    .ListeningCommands(typeof(int)).On(new Endpoint("test", "unistream.u1.commands", true))
                                                    .PublishingEvents(typeof (int)).To(new Endpoint()).RoutedTo(new Endpoint())
                                                    .PublishingEvents(typeof (string)).To(new Endpoint())
                                                    .WithEventStore(dispatchCommits => Wireup.Init()
                                                                                             .LogToOutputWindow()
                                                                                             .UsingInMemoryPersistence()
                                                                                             .InitializeStorageEngine()
                                                                                             .UsingJsonSerialization()
                                                                                             .UsingSynchronousDispatchScheduler()
                                                                                                 .DispatchTo(dispatchCommits))
                                                ); */



                //  messagingEngine.Send("test", new Endpoint("test", "unistream.u1.commands", true,"json"));
                cqrsEngine.SendCommand("test", "integration");
                Thread.Sleep(3000);
            }
        }


        [Test]
        [Ignore]
        public void NEventSToreInvestigationTest()
        {
            var log = MockRepository.GenerateMock<ILog>();
            using (var engine = new InMemoryCqrsEngine(
                LocalBoundedContext.Named("local")
                                   .PublishingEvents(typeof (int)).To("events").RoutedTo("events")
                                   .ListeningCommands(typeof (string)).On("commands1").RoutedFromSameEndpoint()
                                   .ListeningCommands(typeof (DateTime)).On("commands2").RoutedFromSameEndpoint()
                                   .WithCommandsHandler<CommandHandler>()
                                   .WithProcess<TestProcess>()
                                   .WithEventStore(dispatchCommits => Wireup.Init()
                                                                            .LogTo(type => log)
                                                                            .UsingInMemoryPersistence()
                                                                            .InitializeStorageEngine()
                                                                            .UsingJsonSerialization()
                                                                            .UsingSynchronousDispatchScheduler()
                                                                            .DispatchTo(dispatchCommits))
                ,
                LocalBoundedContext.Named("projections")
                                   .WithProjection<EventsListener>("local"),
                RemoteBoundedContext.Named("remote")
                                    .ListeningCommands(typeof (object)).On("remoteCommands")
                                    .PublishingEvents(typeof (int)).To("remoteEvents"),
                Saga<TestSaga>.Listening("local", "projections"),
                Saga.Instance(new TestSaga()).Listening("local", "projections")
                ))
            {
                /*
                                                                                .WithEventSource()
                                                                                .WithAggregates()
                                                                                .WithDocumentStore());
                             * */

                engine.SendCommand("test", "local");
                engine.SendCommand(DateTime.Now, "local");

                Thread.Sleep(500);
                Console.WriteLine("Disposing...");
            }
            Console.WriteLine("Dispose completed.");
        }

        [Test]
        [Ignore("Does not work on tc")]
        public void EventStoreTest()
        {
            var log = MockRepository.GenerateMock<ILog>();
            var eventStoreConnection = EventStoreConnection.Create(ConnectionSettings.Default,
                                                                   new IPEndPoint(IPAddress.Loopback, 1113));
            eventStoreConnection.Connect();
            using (var engine = new InMemoryCqrsEngine(
                LocalBoundedContext.Named("local")
                                   .PublishingEvents(typeof (int), typeof (TestAggregateRootNameChangedEvent),
                                                     typeof (TestAggregateRootCreatedEvent))
                                   .To("events")
                                   .RoutedTo("events")
                                   .ListeningCommands(typeof (string)).On("commands1").RoutedFromSameEndpoint()
                                   .WithCommandsHandler<CommandHandler>()
                                   .WithProcess<TestProcess>()
                                   .WithEventStore(dispatchCommits => Wireup.Init()
                                                                            .LogTo(type => log)
                                                                            .UsingInMemoryPersistence()
                                                                            .InitializeStorageEngine()
                                                                            .UsingJsonSerialization()
                                                                            .UsingSynchronousDispatchScheduler()
                                                                            .DispatchTo(dispatchCommits))
                ))
            {
                engine.SendCommand("test", "local");

                Thread.Sleep(500);
                Console.WriteLine("Disposing...");
            }
            Console.WriteLine("Dispose completed.");
        }


        [Test]
        [Ignore]
        public void InvestigationTest()
        {
            long count = 0;
            var handled = new AutoResetEvent(false);
            using (var engine = new MessagingEngine(
                new TransportResolver(new Dictionary<string, TransportInfo>
                    {
                        {"tr", new TransportInfo("localhost", "guest", "guest", "None",messaging:"RabbitMq")}
                    }),new RabbitMqTransportFactory()))
            {
                var eventStoreConnection = EventStoreConnection.Create(ConnectionSettings.Create().UseConsoleLogger().SetDefaultUserCredentials(new UserCredentials("admin", "changeit")),
                                                                       new IPEndPoint(IPAddress.Loopback, 1113));
                eventStoreConnection.Connect();

              
                eventStoreConnection.SubscribeToAllFrom(Position.Start, false, (subscription, @event) =>
                    {
                        engine.Send(@event, new Endpoint("tr", "t1", true, "json"));
                        handled.Set();
                        count++;
                    }, subscription => { });

                handled.WaitOne();
                var sw = Stopwatch.StartNew();
                while (handled.WaitOne(100))
                {
                }
                Console.WriteLine("Published {0} events. Within {1}ms", count, sw.ElapsedMilliseconds);
            }
        }

        [Test]
        [Ignore]
        public void InvestigationTest1()
        {
            var eventStoreConnection = EventStoreConnection.Create(ConnectionSettings.Create().UseConsoleLogger().SetDefaultUserCredentials(new UserCredentials("admin","changeit")),
                                                                   new IPEndPoint(IPAddress.Loopback, 1113));
            eventStoreConnection.Connect();
            eventStoreConnection.SubscribeToStreamFrom("$stats-127.0.0.1:2113", 0, false, (subscription, @event) =>
                {
                    Console.WriteLine(".");
                }, subscription => { Console.WriteLine(subscription); });

            Thread.Sleep(10000);
        }
    

        [Test]
        [Ignore("Does not work on tc")]
        //[TestCase(true, TestName = "GetEventStore")]
        [TestCase(false, TestName = "NEventStore")]
        public void GetEventStoreTest(bool getES)
        {

            var log = MockRepository.GenerateMock<ILog>();
            var eventsListener = new EventsListener();
            var localBoundedContext = LocalBoundedContext.Named("local")
                                    .PublishingEvents(typeof (TestAggregateRootNameChangedEvent), typeof (TestAggregateRootCreatedEvent)).To("events").RoutedTo("events")
                                    .ListeningCommands(typeof (string)).On("commands1").RoutedFromSameEndpoint()
                                    .WithCommandsHandler<EsCommandHandler>();
            if (getES)
            {
                var eventStoreConnection = EventStoreConnection.Create(ConnectionSettings.Default, new IPEndPoint(IPAddress.Loopback, 1113));
                eventStoreConnection.Connect();
                localBoundedContext.WithEventStore(eventStoreConnection);
            }
            else
                localBoundedContext.WithEventStore(dispatchCommits => Wireup.Init()
                    .LogTo(type => log)
                    .UsingInMemoryPersistence()
                    .InitializeStorageEngine()
                    .UsingJsonSerialization()
                    .UsingSynchronousDispatchScheduler()
                    .DispatchTo(dispatchCommits));
            using (var engine = new InMemoryCqrsEngine(localBoundedContext,LocalBoundedContext.Named("projections").WithProjection(eventsListener,"local")))
            {
                var guid = Guid.NewGuid();
                engine.SendCommand(guid+":create", "local");
                engine.SendCommand(guid + ":changeName:newName", "local");

                Thread.Sleep(5000);
                Console.WriteLine("Disposing...");
            }

            Assert.That(eventsListener.Handled.Select(e=>e.GetType()).ToArray(),Is.EqualTo( new[]{typeof(TestAggregateRootCreatedEvent), typeof(TestAggregateRootNameChangedEvent)}),"Events were not stored or published");
            Console.WriteLine("Dispose completed.");
        }


        [Test]
        [Ignore]
        public void ReplayEventsRmqTest()
        {
            var endpointResolver = MockRepository.GenerateMock<IEndpointResolver>();
            endpointResolver.Expect(r => r.Resolve("local","commands")).Return(new Endpoint("rmq", "commandsExchange", "commands", true, "json"));
            endpointResolver.Expect(r => r.Resolve("local", "events")).Return(new Endpoint("rmq", "eventsExchange", "events", true, "json"));


            var transports = new Dictionary<string, TransportInfo> { { "rmq", new TransportInfo("localhost", "guest", "guest", null, "RabbitMq") } };
            var messagingEngine = new MessagingEngine(new TransportResolver(transports), new RabbitMqTransportFactory())
            {
                Logger = new ConsoleLogger()
            };


            var eventsListener = new EventsListener();
            var localBoundedContext = LocalBoundedContext.Named("local")
                .PublishingEvents(typeof(TestAggregateRootNameChangedEvent), typeof(TestAggregateRootCreatedEvent)).To("events").RoutedTo("events")
                .ListeningCommands(typeof(string)).On("commands").RoutedFromSameEndpoint()
                .ListeningInfrastructureCommands().On("commands").RoutedFromSameEndpoint()
                .WithCommandsHandler<EsCommandHandler>()
                .WithEventStore(dispatchCommits => Wireup.Init()
                    .LogToOutputWindow()
                    .UsingInMemoryPersistence()
                    .InitializeStorageEngine()
                    .UsingJsonSerialization()
                    .UsingSynchronousDispatchScheduler()
                    .DispatchTo(dispatchCommits));
            using (messagingEngine)
            {
                using (
                    var engine = new CqrsEngine(messagingEngine, endpointResolver, localBoundedContext,
                        LocalBoundedContext.Named("projections").WithProjection(eventsListener, "local")))
                {
                    var guid=Guid.NewGuid();
                    engine.SendCommand(guid + ":create", "local");
                    engine.SendCommand(guid + ":changeName:newName", "local");

                    Thread.Sleep(2000);
                    //engine.SendCommand(new ReplayEventsCommand { Destination = "events", From = DateTime.MinValue }, "local");
                    engine.ReplayEvents("local");
                    Thread.Sleep(2000);
                    Console.WriteLine("Disposing...");
                }
            }


            Assert.That(eventsListener.Handled.Count, Is.EqualTo(4), "Events were not redelivered");

        }
        [Test]
        [Ignore("does not work on TC")]
        [TestCase(new Type[0],2,TestName = "AllEvents")]
        [TestCase(new []{typeof(TestAggregateRootNameChangedEvent)},1,TestName = "FilteredEvents")]
        public void ReplayEventsTest(Type[] types,int expectedReplayCount)
        {
            var log = MockRepository.GenerateMock<ILog>();
            var eventsListener = new EventsListener();
            var localBoundedContext = LocalBoundedContext.Named("local")
                .PublishingEvents(typeof(TestAggregateRootNameChangedEvent), typeof(TestAggregateRootCreatedEvent)).To("events").RoutedTo("events")
                .ListeningCommands(typeof(string)).On("commands").RoutedFromSameEndpoint()
                .ListeningInfrastructureCommands().On("commands").RoutedFromSameEndpoint()
                .WithCommandsHandler<EsCommandHandler>()
                .WithEventStore(dispatchCommits => Wireup.Init()
                    .LogTo(type => log)
                    .UsingInMemoryPersistence()
                    .InitializeStorageEngine()
                    .UsingJsonSerialization()
                    .UsingSynchronousDispatchScheduler()
                    .DispatchTo(dispatchCommits));

                using (
                    var engine = new InMemoryCqrsEngine(localBoundedContext,
                        LocalBoundedContext.Named("projections").WithProjection(eventsListener, "local")))
                {
                    var guid = Guid.NewGuid();
                    engine.SendCommand(guid + ":create", "local");
                    engine.SendCommand(guid + ":changeName:newName", "local");


                    Thread.Sleep(2000);
                    //engine.SendCommand(new ReplayEventsCommand { Destination = "events", From = DateTime.MinValue }, "local");
                    engine.ReplayEvents("local", types);
                    Thread.Sleep(2000);
                    Console.WriteLine("Disposing...");
                }

            Assert.That(eventsListener.Handled.Count, Is.EqualTo(2+expectedReplayCount), "Wrong number of events was replayed");

        }


    }

    public class TestProcess:IProcess
    {
        public void Start(ICommandSender commandSender, IEventPublisher eventPublisher)
        {
            Console.WriteLine("Test process started");
        }

        public void Dispose()
        {
            Console.WriteLine("Test process disposed");
        }
    }

    public class TestSaga
    {
        private void Handle(int @event , ICommandSender engine, string boundedContext)
        {
            Console.WriteLine("Event cought by saga:"+@event);
        }
    }


}