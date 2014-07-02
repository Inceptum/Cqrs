using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reactive.Disposables;
using System.Threading;
using CommonDomain;
using CommonDomain.Core;
using CommonDomain.Persistence;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using Inceptum.Cqrs.Configuration;
using Inceptum.Cqrs.Routing;
using Inceptum.Messaging;
using Inceptum.Messaging.Configuration;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.RabbitMq;
using NEventStore;
using NEventStore.Dispatcher;
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
                                                   Register.DefaultEndpointResolver(new InMemoryEndpointResolver()),
                                                   Register.BoundedContext("bc")
                                                                      .PublishingEvents(typeof (int)).With("eventExchange") 
                                                                      .ListeningCommands(typeof (string)).On("exchange1") 
                                                                      .ListeningCommands(typeof (string)).On("exchange2") 
                                                                      .WithCommandsHandler(commandHandler))
                    )
                {
                    messagingEngine.Send("test1", new Endpoint("InMemory", "exchange1", serializationFormat: "json"));
                    messagingEngine.Send("test2", new Endpoint("InMemory", "exchange2", serializationFormat: "json"));
                    messagingEngine.Send("test3", new Endpoint("InMemory", "exchange3", serializationFormat: "json"));
                    Thread.Sleep(2000);
                    Assert.That(commandHandler.AcceptedCommands, Is.EquivalentTo(new[] { "test1", "test2" }));
                }
            }
        }  

        [Test]
        public void ContextUsesDefaultRouteForCommandPublishingIfItDoesNotHaveItsOwnTest()
        {
            var bcCommands = new Endpoint("InMemory", "bcCommands",serializationFormat:"json");
            var defaultCommands = new Endpoint("InMemory", "defaultCommands", serializationFormat: "json");
            using (
                var messagingEngine =
                    new MessagingEngine(
                        new TransportResolver(new Dictionary<string, TransportInfo>
                            {
                                {"InMemory", new TransportInfo("none", "none", "none", null, "InMemory")}
                            })))
            {
                using (var engine = new CqrsEngine(messagingEngine,
                                                                      Register.DefaultEndpointResolver(new InMemoryEndpointResolver()),
                                                                      Register.BoundedContext("bc2")
                                                                        .PublishingCommands(typeof(int)).To("bc1").With("bcCommands"),
                                                                      Register.DefaultRouting
                                                                        .PublishingCommands(typeof(string)).To("bc1").With("defaultCommands")
                                                                        .PublishingCommands(typeof(int)).To("bc1").With("defaultCommands")
                                                                      )
                    )
                {
                    var received=new AutoResetEvent(false);
                    using (messagingEngine.Subscribe(defaultCommands, o => received.Set(), s => { }, typeof(string)))
                    {
                        engine.SendCommand("test", "bc2", "bc1");
                        Assert.That(received.WaitOne(1000), Is.True, "not defined for context command was not routed with default route map");
                    }

                    using (messagingEngine.Subscribe(bcCommands, o => received.Set(),s => {},typeof(int)))
                    {
                        engine.SendCommand(1, "bc2", "bc1");
                        Assert.That(received.WaitOne(1000), Is.True, "defined for context command was not routed with context route map");
                    }
                }
            }
        }

        [Test]
        public void SagaTest()
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
                                                   Register.DefaultEndpointResolver(new InMemoryEndpointResolver()),
                                                   Register.BoundedContext("bc1")
                                                        .PublishingEvents(typeof (int)).With("events1").WithLoopback()
                                                        .ListeningCommands(typeof(string)).On("commands1").WithLoopback()
                                                        .WithCommandsHandler<CommandHandler>(),
                                                   Register.BoundedContext("bc2")
                                                        .PublishingEvents(typeof (int)).With("events2").WithLoopback()
                                                        .ListeningCommands(typeof(string)).On("commands2").WithLoopback()
                                                        .WithCommandsHandler<CommandHandler>(),
                                                   Register.Saga<TestSaga>("SomeIntegration")
                                                        .ListeningEvents(typeof(int)).From("bc1").On("events1")
                                                        .ListeningEvents(typeof(int)).From("bc2").On("events2")
                                                        .PublishingCommands(typeof(string)).To("bc2").With("commands2")
                                                        .ProcessingOptions("commands").MultiThreaded(5))
                    )
                {
                    messagingEngine.Send("cmd", new Endpoint("InMemory", "commands1", serializationFormat: "json"));
                  
                    Assert.That(TestSaga.Complete.WaitOne(1000), Is.True, "Saga has not got events or failed to send command");
                }
            }
        }



       

        [Test]
        public void FluentApiTest()
        {
            var endpointProvider = MockRepository.GenerateMock<IEndpointProvider>();
            endpointProvider.Expect(r => r.Get("high")).Return(new Endpoint("InMemory", "high", true, "json"));
            endpointProvider.Expect(r => r.Get("low")).Return(new Endpoint("InMemory", "low", true, "json"));
            endpointProvider.Expect(r => r.Get("medium")).Return(new Endpoint("InMemory", "medium", true, "json"));

            var messagingEngine =
                new MessagingEngine(
                    new TransportResolver(new Dictionary<string, TransportInfo>
                    {
                        {"InMemory", new TransportInfo("none", "none", "none", null, "InMemory")},
                        {"rmq", new TransportInfo("none", "none", "none", null, "InMemory")}
                    }));
            using (messagingEngine)
            {
                
            
            new CqrsEngine(messagingEngine,endpointProvider,
                Register.BoundedContext("bc")
                    .PublishingCommands(typeof(string)).To("operations").With("operationsCommandsRoute")
                    .ListeningEvents(typeof(int)).From("operations").On("operationEventsRoute")
                    .ListeningCommands(typeof(string)).On("commandsRoute")
                        //same as .PublishingCommands(typeof(string)).To("bc").With("selfCommandsRoute")  
                        .WithLoopback("selfCommandsRoute")
                    .PublishingEvents(typeof(int)).With("eventsRoute")
                        //same as.ListeningEvents(typeof(int)).From("bc").On("selfEventsRoute")
                        .WithLoopback("selfEventsRoute")



                    //explicit prioritization 
                    .ListeningCommands(typeof(string)).On("explicitlyPrioritizedCommandsRoute")
                        .Prioritized(lowestPriority: 2)
                            .WithEndpoint("high").For(key=>key.Priority==0)
                            .WithEndpoint("medium").For(key=>key.Priority==1)
                            .WithEndpoint("low").For(key=>key.Priority==2)

                    //resolver based prioritization 
                    .ListeningCommands(typeof(string)).On("prioritizedCommandsRoute")
                        .Prioritized(lowestPriority: 2)
                        .WithEndpointResolver(new InMemoryEndpointResolver())
                    .ProcessingOptions("explicitlyPrioritizedCommandsRoute").MultiThreaded(10)
                    .ProcessingOptions("prioritizedCommandsRoute").MultiThreaded(10).QueueCapacity(1024),
                Register.Saga<TestSaga>("saga")
                    .PublishingCommands(typeof(string)).To("operations").With("operationsCommandsRoute")
                    .ListeningEvents(typeof(int)).From("operations").On("operationEventsRoute"),
                Register.DefaultRouting
                    .PublishingCommands(typeof(string)).To("operations").With("defaultCommandsRoute")
                    .PublishingCommands(typeof(int)).To("operations").With("defaultCommandsRoute"),
                Register.DefaultEndpointResolver(new RabbitMqConventionEndpointResolver("rmq", "json"))
               );
            }
        }


        [Test]
        public void PrioritizedCommandsProcessingTest()
        {
            var endpointProvider = MockRepository.GenerateMock<IEndpointProvider>();
            endpointProvider.Expect(r => r.Get("exchange1")).Return(new Endpoint("InMemory", "bc.exchange1", true, "json"));
            endpointProvider.Expect(r => r.Get("exchange2")).Return(new Endpoint("InMemory", "bc.exchange2", true, "json"));
            using (
                var messagingEngine =
                    new MessagingEngine(
                        new TransportResolver(new Dictionary<string, TransportInfo>
                            {
                                {"InMemory", new TransportInfo("none", "none", "none", null, "InMemory")}
                            })))
            {
                var commandHandler = new CommandHandler(100);
                using (var engine = new CqrsEngine(messagingEngine,endpointProvider,
                                                   Register.DefaultEndpointResolver(new InMemoryEndpointResolver()),
                                                   Register.BoundedContext("bc")
                                                    .PublishingEvents(typeof (int)).With("eventExchange").WithLoopback("eventQueue")
                                                    .ListeningCommands(typeof(string)).On("commandsRoute")
                                                        .Prioritized(lowestPriority: 1)
                                                            .WithEndpoint("exchange1").For(key => key.Priority == 1)
                                                            .WithEndpoint("exchange2").For(key => key.Priority == 2)
                                                    .ProcessingOptions("commandsRoute").MultiThreaded(2)
                                                    .WithCommandsHandler(commandHandler))
                    )
                {
                    messagingEngine.Send("low1", new Endpoint("InMemory", "bc.exchange2", serializationFormat: "json"));
                    messagingEngine.Send("low2", new Endpoint("InMemory", "bc.exchange2", serializationFormat: "json"));
                    messagingEngine.Send("low3", new Endpoint("InMemory", "bc.exchange2", serializationFormat: "json"));
                    messagingEngine.Send("low4", new Endpoint("InMemory", "bc.exchange2", serializationFormat: "json"));
                    messagingEngine.Send("low5", new Endpoint("InMemory", "bc.exchange2", serializationFormat: "json"));
                    messagingEngine.Send("low6", new Endpoint("InMemory", "bc.exchange2", serializationFormat: "json"));
                    messagingEngine.Send("low7", new Endpoint("InMemory", "bc.exchange2", serializationFormat: "json"));
                    messagingEngine.Send("low8", new Endpoint("InMemory", "bc.exchange2", serializationFormat: "json"));
                    messagingEngine.Send("low9", new Endpoint("InMemory", "bc.exchange2", serializationFormat: "json"));
                    messagingEngine.Send("low10", new Endpoint("InMemory", "bc.exchange2", serializationFormat: "json"));
                    messagingEngine.Send("high", new Endpoint("InMemory", "bc.exchange1", serializationFormat: "json"));
                    Thread.Sleep(2000);
                    Console.WriteLine(string.Join("\n", commandHandler.AcceptedCommands));
                    Assert.That(commandHandler.AcceptedCommands.Take(2).Any(c => (string)c == "high"), Is.True);
                }
            }
        }

        [Test]
        public void EndpointVerificationTest()
        {
            var endpointProvider = MockRepository.GenerateMock<IEndpointProvider>();
            endpointProvider.Expect(p => p.Contains(null)).IgnoreArguments().Return(false);
         
            var messagingEngine = MockRepository.GenerateStrictMock<IMessagingEngine>();
            messagingEngine.Expect(e => e.AddProcessingGroup(Arg<string>.Is.Equal("cqrs.operations.localEvents"), Arg<ProcessingGroupInfo>.Is.Anything));
            messagingEngine.Expect(e => e.AddProcessingGroup(Arg<string>.Is.Equal("cqrs.operations.remoteEvents"), Arg<ProcessingGroupInfo>.Is.Anything));
            messagingEngine.Expect(e => e.AddProcessingGroup(Arg<string>.Is.Equal("cqrs.operations.localCommands"), Arg<ProcessingGroupInfo>.Is.Anything));
            messagingEngine.Expect(e => e.AddProcessingGroup(Arg<string>.Is.Equal("cqrs.operations.remoteCommands"), Arg<ProcessingGroupInfo>.Is.Anything));
            messagingEngine.Expect(e => e.AddProcessingGroup(Arg<string>.Is.Equal("cqrs.SomeIntegration.sagaEvents"), Arg<ProcessingGroupInfo>.Is.Anything));
            messagingEngine.Expect(e => e.AddProcessingGroup(Arg<string>.Is.Equal("cqrs.SomeIntegration.sagaCommands"), Arg<ProcessingGroupInfo>.Is.Anything));
            messagingEngine.Expect(e => e.AddProcessingGroup(Arg<string>.Is.Equal("cqrs.default.defaultCommands"), Arg<ProcessingGroupInfo>.Is.Anything));
            messagingEngine.Expect(e => e.AddProcessingGroup(Arg<string>.Is.Equal("cqrs.operations.prioritizedCommands"), Arg<ProcessingGroupInfo>.Matches(info => info.ConcurrencyLevel==2)));
            string error;
            messagingEngine.Expect(e => e.VerifyEndpoint(new Endpoint(), EndpointUsage.None, false, out error)).IgnoreArguments().Return(true).Repeat.Times(18);
            //subscription for remote events
            messagingEngine.Expect(e => e.Subscribe(
                Arg<Endpoint>.Is.Anything,
                Arg<CallbackDelegate<object>>.Is.Anything, 
                Arg<Action<string, AcknowledgeDelegate>>.Is.Anything,
                Arg<string>.Is.Equal("cqrs.operations.remoteEvents"),
                Arg<int>.Is.Equal(0),
                Arg<Type[]>.List.Equal(new []{typeof(int),typeof(long)}))).Return(Disposable.Empty);       
            
            //subscription for local events
            messagingEngine.Expect(e => e.Subscribe(
                Arg<Endpoint>.Is.Anything,
                Arg<CallbackDelegate<object>>.Is.Anything, 
                Arg<Action<string, AcknowledgeDelegate>>.Is.Anything,
                Arg<string>.Is.Equal("cqrs.operations.localEvents"),
                Arg<int>.Is.Equal(0),
                Arg<Type[]>.List.Equal(new []{typeof(bool)}))).Return(Disposable.Empty);
           
            //subscription for localCommands
            messagingEngine.Expect(e => e.Subscribe(
                Arg<Endpoint>.Is.Anything,
                Arg<CallbackDelegate<object>>.Is.Anything, 
                Arg<Action<string, AcknowledgeDelegate>>.Is.Anything,
                Arg<string>.Is.Equal("cqrs.operations.localCommands"),
                Arg<int>.Is.Equal(0),
                Arg<Type[]>.List.Equal(new[] { typeof(string), typeof(DateTime) }))).Return(Disposable.Empty);
          
            //subscription for prioritizedCommands (priority 1 and 2)
            messagingEngine.Expect(e => e.Subscribe(
                Arg<Endpoint>.Is.Anything,
                Arg<CallbackDelegate<object>>.Is.Anything, 
                Arg<Action<string, AcknowledgeDelegate>>.Is.Anything,
                Arg<string>.Is.Equal("cqrs.operations.prioritizedCommands"),
                Arg<int>.Is.Equal(1),
                Arg<Type[]>.List.Equal(new[] { typeof(byte) }))).Return(Disposable.Empty);
            messagingEngine.Expect(e => e.Subscribe(
                Arg<Endpoint>.Is.Anything,
                Arg<CallbackDelegate<object>>.Is.Anything, 
                Arg<Action<string, AcknowledgeDelegate>>.Is.Anything,
                Arg<string>.Is.Equal("cqrs.operations.prioritizedCommands"),
                Arg<int>.Is.Equal(2),
                Arg<Type[]>.List.Equal(new[] { typeof(byte) }))).Return(Disposable.Empty);

            //subscription for saga events
            messagingEngine.Expect(e => e.Subscribe(
                Arg<Endpoint>.Is.Anything,
                Arg<CallbackDelegate<object>>.Is.Anything,
                Arg<Action<string, AcknowledgeDelegate>>.Is.Anything,
                Arg<string>.Is.Equal("cqrs.SomeIntegration.sagaEvents"),
                Arg<int>.Is.Equal(0),
                Arg<Type[]>.List.Equal(new[] { typeof(int) }))).Return(Disposable.Empty);       

            
            //send command to remote BC
            messagingEngine.Expect(e => e.Send(
                Arg<object>.Is.Equal("testCommand"),
                Arg<Endpoint>.Is.Anything,
                Arg<string>.Is.Equal("cqrs.operations.remoteCommands")
                ));

            //publish event from local BC
            messagingEngine.Expect(e => e.Send(
                Arg<object>.Is.Equal(true),
                Arg<Endpoint>.Is.Anything,
                Arg<string>.Is.Equal("cqrs.operations.localEvents")
                ));


            using (var ce=new CqrsEngine(messagingEngine,
                    Register.DefaultEndpointResolver(new RabbitMqConventionEndpointResolver("tr1", "protobuf")),
                    Register.BoundedContext("operations")
                        .PublishingEvents(typeof (bool) ).With("localEvents").WithLoopback()
                        .ListeningCommands(typeof(string), typeof(DateTime)).On("localCommands").WithLoopback()
                        .ListeningCommands(typeof(byte)).On("prioritizedCommands").Prioritized(2)
                        .ListeningEvents(typeof(int), typeof(long)).From("integration").On("remoteEvents")
                        .PublishingCommands(typeof(string)).To("integration").With("remoteCommands").Prioritized(5)
                        .ProcessingOptions("prioritizedCommands").MultiThreaded(2),
                   Register.Saga<TestSaga>("SomeIntegration")
                        .ListeningEvents(typeof(int)).From("bc1").On("sagaEvents")
                        .PublishingCommands(typeof(string)).To("bc2").With("sagaCommands")
                       // .ProcessingOptions("commands").MultiThreaded(5)
                       ,
                       Register.DefaultRouting.PublishingCommands(typeof(string)).To("bc3").With("defaultCommands")
                    ))
                {
                    ce.Contexts.Find(bc=>bc.Name=="operations").EventsPublisher.PublishEvent(true);
                    ce.SendCommand("testCommand", "operations", "integration",1);
                }

            messagingEngine.VerifyAllExpectations();
        }

 

 
/*

        [Test]
        [Ignore("Does not work on tc")]
        public void EventStoreTest()
        {
            var log = MockRepository.GenerateMock<ILog>();
            var eventStoreConnection = EventStoreConnection.Create(ConnectionSettings.Default,
                                                                   new IPEndPoint(IPAddress.Loopback, 1113));
            eventStoreConnection.Connect();
            using (var engine = new InMemoryCqrsEngine(
                BoundedConfiguration.Context.BoundedContext("local")
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
                engine.SendCommand("test", "local", "local");

                Thread.Sleep(500);
                Console.WriteLine("Disposing...");
            }
            Console.WriteLine("Dispose completed.");
        }
*/


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
        //[Ignore("Does not work on tc")]
  //      [TestCase(true, TestName = "GetEventStore")]
        [TestCase(false, TestName = "NEventStore")]
        public void GetEventStoreTest(bool getES)
        {

            var log = MockRepository.GenerateMock<ILog>();
            var eventsListener = new EventsListener();
            var localBoundedContext = Register.BoundedContext("local")
                                    .PublishingEvents(typeof (TestAggregateRootNameChangedEvent), typeof (TestAggregateRootCreatedEvent)).With("events").WithLoopback()
                                    .ListeningCommands(typeof (string)).On("commands1").WithLoopback()
                                    .WithCommandsHandler<EsCommandHandler>();
            if (getES)
            {
                var eventStoreConnection = EventStoreConnection.Create(ConnectionSettings.Default, new IPEndPoint(IPAddress.Loopback, 1113));
                eventStoreConnection.Connect();
                localBoundedContext.WithGetEventStore(eventStoreConnection);
            }
            else
            {
                localBoundedContext.WithNEventStore(dispatchCommits => Wireup.Init()
                    .LogTo(type => log)
                    .UsingInMemoryPersistence()
                    .InitializeStorageEngine()
                    .UsingJsonSerialization()
                    .UsingSynchronousDispatchScheduler()
                    .DispatchTo(dispatchCommits));
            }

            using (var engine = new InMemoryCqrsEngine(localBoundedContext,

                Register.BoundedContext("projections")
                            .ListeningEvents(typeof(TestAggregateRootNameChangedEvent), typeof(TestAggregateRootCreatedEvent)).From("local").On("events")
                            .WithProjection(eventsListener, "local")))
            {
                var guid = Guid.NewGuid();
                engine.SendCommand(guid+":create", "local", "local");
                engine.SendCommand(guid + ":changeName:newName", "local", "local");

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
            endpointResolver.Expect(r => r.Resolve(Arg<string>.Is.Equal("commands"), Arg<RoutingKey>.Matches(k => k.MessageType == typeof(string) && k.RouteType==RouteType.Commands), Arg<IEndpointProvider>.Is.Anything)).Return(new Endpoint("rmq", "commandsExchange", "commands", true, "json"));
            endpointResolver.Expect(r => r.Resolve(Arg<string>.Is.Equal("events"), Arg<RoutingKey>.Matches(k => k.MessageType == typeof(TestAggregateRootNameChangedEvent) && k.RouteType == RouteType.Events), Arg<IEndpointProvider>.Is.Anything)).Return(new Endpoint("rmq", "eventsExchange", "events", true, "json"));
            endpointResolver.Expect(r => r.Resolve(Arg<string>.Is.Equal("events"), Arg<RoutingKey>.Matches(k => k.MessageType == typeof(TestAggregateRootCreatedEvent) && k.RouteType == RouteType.Events), Arg<IEndpointProvider>.Is.Anything)).Return(new Endpoint("rmq", "eventsExchange", "events", true, "json"));

            var transports = new Dictionary<string, TransportInfo> { { "rmq", new TransportInfo("localhost", "guest", "guest", null, "RabbitMq") } };
            var messagingEngine = new MessagingEngine(new TransportResolver(transports), new RabbitMqTransportFactory());


            var eventsListener = new EventsListener();
            var localBoundedContext = Register.BoundedContext("local")
                .PublishingEvents(typeof(TestAggregateRootNameChangedEvent), typeof(TestAggregateRootCreatedEvent)).With("events").WithLoopback()
                .ListeningCommands(typeof(string)).On("commands").WithLoopback()
                .ListeningInfrastructureCommands().On("commands").WithLoopback()
                .WithCommandsHandler<EsCommandHandler>()
                .WithNEventStore(dispatchCommits => Wireup.Init()
                    .LogToOutputWindow()
                    .UsingInMemoryPersistence()
                    .InitializeStorageEngine()
                    .UsingJsonSerialization()
                    .UsingSynchronousDispatchScheduler()
                    .DispatchTo(dispatchCommits));
            using (messagingEngine)
            {
                using (
                    var engine = new CqrsEngine(messagingEngine,  localBoundedContext,
                        Register.DefaultEndpointResolver(endpointResolver),
                        Register.BoundedContext("projections").WithProjection(eventsListener, "local")))
                {
                    var guid=Guid.NewGuid();
                    engine.SendCommand(guid + ":create", "local", "local");
                    engine.SendCommand(guid + ":changeName:newName", "local", "local");

                    Thread.Sleep(2000);
                    //engine.SendCommand(new ReplayEventsCommand { Destination = "events", From = DateTime.MinValue }, "local");
                    engine.ReplayEvents("local", "local", DateTime.MinValue);
                    Thread.Sleep(2000);
                    Console.WriteLine("Disposing...");
                }
            }


            Assert.That(eventsListener.Handled.Count, Is.EqualTo(4), "Events were not redelivered");

        }
        [Test]
     //   [Ignore("does not work on TC")]
        [TestCase(new Type[0],2,TestName = "AllEvents")]
        [TestCase(new []{typeof(TestAggregateRootNameChangedEvent)},1,TestName = "FilteredEvents")]
        public void ReplayEventsTest(Type[] types,int expectedReplayCount)
        {
            var log = MockRepository.GenerateMock<ILog>();
            var eventsListener = new EventsListener();
            var localBoundedContext = Register.BoundedContext("local")
                .PublishingEvents(typeof(TestAggregateRootNameChangedEvent), typeof(TestAggregateRootCreatedEvent)).With("events").WithLoopback()
                .ListeningCommands(typeof(string)).On("commands").WithLoopback()
                .ListeningInfrastructureCommands().On("commands").WithLoopback()
                .WithCommandsHandler<EsCommandHandler>()
                .WithNEventStore(dispatchCommits => Wireup.Init()
                    .LogTo(type => log)
                    .UsingInMemoryPersistence()
                    .InitializeStorageEngine()
                    .UsingJsonSerialization()
                    .UsingSynchronousDispatchScheduler()
                    .DispatchTo(dispatchCommits));

                using (
                    var engine = new InMemoryCqrsEngine(localBoundedContext,
                        Register.BoundedContext("projections")
                        .ListeningEvents(typeof(TestAggregateRootNameChangedEvent), typeof(TestAggregateRootCreatedEvent)).From("local").On("events")
                            .WithProjection(eventsListener, "local")
                            .PublishingInfrastructureCommands().To("local").With("commands")))
                {
                    var guid = Guid.NewGuid();
                    engine.SendCommand(guid + ":create", "local", "local");
                    engine.SendCommand(guid + ":changeName:newName", "local", "local");


                    Thread.Sleep(2000);
                    //engine.SendCommand(new ReplayEventsCommand { Destination = "events", From = DateTime.MinValue }, "local");
                    engine.ReplayEvents("projections", "local", DateTime.MinValue, types);
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


}