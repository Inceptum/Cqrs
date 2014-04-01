using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Castle.Core.Logging;
using Castle.Facilities.Startable;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using CommonDomain.Persistence;
using Inceptum.Cqrs.Castle;
using Inceptum.Cqrs.Configuration;
using Inceptum.Cqrs.InfrastructureCommands;
using Inceptum.Cqrs.Routing;
using Inceptum.Messaging.Configuration;
using Inceptum.Messaging.Contract;
using NEventStore;
using NEventStore.Logging;
using NUnit.Framework;
using Rhino.Mocks;

namespace Inceptum.Cqrs.Tests
{
    class RepositoryDependentComponent
    {
        public IRepository Repository { get; set; }

        public RepositoryDependentComponent(IRepository repository)
        {
            Repository = repository;
        }
    }

    internal class CommandsHandler
    {
        public readonly List<object> HandledCommands = new List<object>();

        public CommandsHandler()
        {
            Console.WriteLine();
        }

        private void Handle(string m)
        {
            Console.WriteLine("Command received:" + m);
            HandledCommands.Add(m);
        }

        private CommandHandlingResult Handle(int m)
        {
            Console.WriteLine("Command received:" + m);
            HandledCommands.Add(m);
            return new CommandHandlingResult { Retry = true, RetryDelay = 100 };
        }

        private CommandHandlingResult Handle(RoutedCommand<DateTime> m)
        {
            Console.WriteLine("Command received:" + m.Command + " Origination Endpoint:" + m.OriginEndpoint);
            HandledCommands.Add(m);
            return new CommandHandlingResult { Retry = true, RetryDelay = 100 };
        }

        private void Handle(long m)
        {
            Console.WriteLine("Command received:" + m);
            throw new Exception();
        }
    }


    internal class EventListener
    {
        public readonly List<Tuple<string, string>> EventsWithBoundedContext = new List<Tuple<string, string>>();
        public readonly List<string> Events = new List<string>();

        void Handle(string m, string boundedContext)
        {
            EventsWithBoundedContext.Add(Tuple.Create(m, boundedContext));
            Console.WriteLine(boundedContext + ":" + m);
        }
        void Handle(string m)
        {
            Events.Add(m);
            Console.WriteLine(m);
        }
    }


    class CqrEngineDependentComponent
    {
        public static bool Started { get; set; }
        public CqrEngineDependentComponent(ICqrsEngine engine)
        {
        }
        public void Start()
        {
            Started = true;
        }
    }

    [TestFixture]
    public class CqrsFacilityTests
    {


        [Test]
        [ExpectedException(ExpectedMessage = "Component can not be projection and commands handler simultaneousely")]
        public void ComponentCanNotBeProjectionAndCommandsHandlerSimultaneousely()
        {
            using (var container = new WindsorContainer())
            {
                container.AddFacility<CqrsFacility>(f => f.RunInMemory().Contexts(Register.BoundedContext("bc")));
                container.Register(Component.For<CommandsHandler>().AsCommandsHandler("bc").AsProjection("bc", "remote"));
            }
        }


        [Test]
        public void CqrsEngineIsResolvableAsDependencyOnlyAfterBootstrapTest()
        {
            bool reslovedCqrsDependentComponentBeforeInit = false;
            using (var container = new WindsorContainer())
            {
                container.Register(Component.For<CqrEngineDependentComponent>());
                container.AddFacility<CqrsFacility>(f => f.RunInMemory());
                try
                {
                    container.Resolve<CqrEngineDependentComponent>();
                    reslovedCqrsDependentComponentBeforeInit = true;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                container.Resolve<ICqrsEngineBootstrapper>().Start();

                container.Resolve<CqrEngineDependentComponent>();
                Assert.That(reslovedCqrsDependentComponentBeforeInit, Is.False, "ICqrsEngine was resolved as dependency before it was initialized");
            }
        }

        [Test]
        public void CqrsEngineIsResolvableAsDependencyOnlyAfterBootstrapStartableFacilityTest()
        {
            using (var container = new WindsorContainer())
            {
                container.AddFacility<CqrsFacility>(f => f.RunInMemory())
                    .AddFacility<StartableFacility>(); // (f => f.DeferredTryStart());
                container.Register(Component.For<IMessagingEngine>().Instance(MockRepository.GenerateMock<IMessagingEngine>()));
                container.Register(Component.For<CqrEngineDependentComponent>().StartUsingMethod("Start"));
                Assert.That(CqrEngineDependentComponent.Started, Is.False, "Component was started before commandSender initialization");
                container.Resolve<ICqrsEngineBootstrapper>().Start();
                Assert.That(CqrEngineDependentComponent.Started, Is.True, "Component was not started after commandSender initialization");
            }
        }

        [Test]
        public void ProjectionWiringTest()
        {
            using (var container = new WindsorContainer())
            {
                container.Register(Component.For<IMessagingEngine>().Instance(MockRepository.GenerateMock<IMessagingEngine>()))
                    .AddFacility<CqrsFacility>(f => f.RunInMemory().Contexts(
                            Register.BoundedContext("local").ListeningEvents(typeof(string)).From("remote").On("remoteEVents")
                            ))
                    .Register(Component.For<EventListener>().AsProjection("local", "remote"))
                    .Resolve<ICqrsEngineBootstrapper>().Start();

                var cqrsEngine = (CqrsEngine)container.Resolve<ICqrsEngine>();
                var eventListener = container.Resolve<EventListener>();
                cqrsEngine.Contexts.First(c => c.Name == "local").EventDispatcher.Dispacth("remote", "test", (delay, acknowledge) => { });
                Assert.That(eventListener.EventsWithBoundedContext, Is.EquivalentTo(new[] { Tuple.Create("test", "remote") }), "Event was not dispatched");
                Assert.That(eventListener.Events, Is.EquivalentTo(new[] { "test" }), "Event was not dispatched");
            }
        }

        [Test]
        public void CommandsHandlerWiringTest()
        {
            using (var container = new WindsorContainer())
            {
                container
                    .Register(Component.For<IMessagingEngine>().Instance(MockRepository.GenerateMock<IMessagingEngine>()))
                    .AddFacility<CqrsFacility>(f => f.RunInMemory().Contexts(Register.BoundedContext("bc")))
                    .Register(Component.For<CommandsHandler>().AsCommandsHandler("bc").LifestyleSingleton())
                    .Resolve<ICqrsEngineBootstrapper>().Start();
                var cqrsEngine = (CqrsEngine)container.Resolve<ICqrsEngine>();
                var commandsHandler = container.Resolve<CommandsHandler>();
                cqrsEngine.Contexts.First(c => c.Name == "bc").CommandDispatcher.Dispatch("test", (delay, acknowledge) => { }, new Endpoint(), "route");
                Thread.Sleep(1300);
                Assert.That(commandsHandler.HandledCommands, Is.EqualTo(new[] { "test" }), "Command was not dispatched");
            }
        }


        [Test]
        [Ignore("")]
        public void DependencyOnICommandSenderTest()
        {
            using (var container = new WindsorContainer())
            {
                var messagingEngine = MockRepository.GenerateMock<IMessagingEngine>();
                container
                    .Register(Component.For<IMessagingEngine>().Instance(messagingEngine))
                    .AddFacility<CqrsFacility>(f => f.RunInMemory().Contexts(
                            Register.BoundedContext("bc").ListeningCommands(typeof(string)).On("cmd").WithLoopback())
                            )
                    //    .Register(Component.For<EventListenerWithICommandSenderDependency>().AsSaga("bc", "remoteBc"))
                    .Register(Component.For<CommandsHandler>().AsCommandsHandler("bc"))
                    .Resolve<ICqrsEngineBootstrapper>().Start();

                var cqrsEngine = (CqrsEngine)container.Resolve<ICqrsEngine>();
                cqrsEngine.Contexts.First(c => c.Name == "bc").EventDispatcher.Dispacth("remoteBc", "some event", (delay, acknowledge) => { });

                var listener = container.Resolve<EventListenerWithICommandSenderDependency>();
                var commandsHandler = container.Resolve<CommandsHandler>();
                Assert.That(listener.Sender, Is.Not.Null);
                listener.Sender.SendCommand("test", "bc");
                Thread.Sleep(200);
                Assert.That(commandsHandler.HandledCommands, Is.EqualTo(new[] { "test" }), "Command was not dispatched");
            }
        }

        [Test]
        public void CommandsHandlerWithResultWiringTest()
        {
            using (var container = new WindsorContainer())
            {
                container
                    .Register(Component.For<IMessagingEngine>().Instance(MockRepository.GenerateMock<IMessagingEngine>()))
                    .AddFacility<CqrsFacility>(f => f.RunInMemory().Contexts(Register.BoundedContext("bc")))
                    .Register(Component.For<CommandsHandler>().AsCommandsHandler("bc"))
                    .Resolve<ICqrsEngineBootstrapper>().Start();
                var cqrsEngine = (CqrsEngine)container.Resolve<ICqrsEngine>();
                var commandsHandler = container.Resolve<CommandsHandler>();

                bool acknowledged = false;
                long retrydelay = 0;
                cqrsEngine.Contexts.First(c => c.Name == "bc").CommandDispatcher.Dispatch(1, (delay, acknowledge) =>
                {
                    retrydelay = delay;
                    acknowledged = acknowledge;
                }, new Endpoint(), "route");
                Thread.Sleep(200);
                Assert.That(commandsHandler.HandledCommands, Is.EqualTo(new[] { 1 }), "Command was not dispatched");
                Assert.That(retrydelay, Is.EqualTo(100));
                Assert.That(acknowledged, Is.EqualTo(false));
            }
        }


        [Test]
        public void CommandsHandlerWithResultAndCommandOriginEndpointWiringTest()
        {
            using (var container = new WindsorContainer())
            {
                container
                    .Register(Component.For<IMessagingEngine>().Instance(MockRepository.GenerateMock<IMessagingEngine>()))
                    .AddFacility<CqrsFacility>(f => f.RunInMemory().Contexts(Register.BoundedContext("bc")))
                    .Register(Component.For<CommandsHandler>().AsCommandsHandler("bc"))
                    .Resolve<ICqrsEngineBootstrapper>().Start();
                var cqrsEngine = (CqrsEngine)container.Resolve<ICqrsEngine>();
                var commandsHandler = container.Resolve<CommandsHandler>();

                bool acknowledged = false;
                long retrydelay = 0;
                var endpoint = new Endpoint();
                var command = DateTime.Now;
                cqrsEngine.Contexts.First(c => c.Name == "bc").CommandDispatcher.Dispatch(command, (delay, acknowledge) =>
                {
                    retrydelay = delay;
                    acknowledged = acknowledge;
                }, endpoint, "route");
                Thread.Sleep(200);
                Assert.That(commandsHandler.HandledCommands.Count, Is.EqualTo(1), "Command was not dispatched");
                Assert.That(commandsHandler.HandledCommands[0], Is.TypeOf<RoutedCommand<DateTime>>(), "Command was not dispatched with wrong type");
                Assert.That(((RoutedCommand<DateTime>)(commandsHandler.HandledCommands[0])).Command, Is.EqualTo(command), "Routed command was not dispatched with wrong command");
                Assert.That(((RoutedCommand<DateTime>)(commandsHandler.HandledCommands[0])).OriginEndpoint, Is.EqualTo(endpoint), "Routed command was dispatched with wrong origin endpoint");
                Assert.That(((RoutedCommand<DateTime>)(commandsHandler.HandledCommands[0])).OriginRoute, Is.EqualTo("route"), "Routed command was dispatched with wrong origin route");
                Assert.That(retrydelay, Is.EqualTo(100));
                Assert.That(acknowledged, Is.EqualTo(false));
            }
        }
        [Test]
        public void FailedCommandHandlerCausesRetryTest()
        {
            using (var container = new WindsorContainer())
            {
                container
                    .Register(Component.For<IMessagingEngine>().Instance(MockRepository.GenerateMock<IMessagingEngine>()))
                    .AddFacility<CqrsFacility>(f => f.RunInMemory().Contexts(Register.BoundedContext("bc").FailedCommandRetryDelay(100)))
                    .Register(Component.For<CommandsHandler>().AsCommandsHandler("bc"))
                    .Resolve<ICqrsEngineBootstrapper>().Start();
                var cqrsEngine = (CqrsEngine)container.Resolve<ICqrsEngine>();
                var commandsHandler = container.Resolve<CommandsHandler>();

                bool acknowledged = false;
                long retrydelay = 0;
                cqrsEngine.Contexts.First(c => c.Name == "bc").CommandDispatcher.Dispatch((long)1, (delay, acknowledge) =>
                {
                    retrydelay = delay;
                    acknowledged = acknowledge;
                }, new Endpoint(), "route");
                Thread.Sleep(200);
                Assert.That(retrydelay, Is.EqualTo(100));
                Assert.That(acknowledged, Is.EqualTo(false));
            }
        }

        [Test]
        public void SagaTest()
        {
            using (var container = new WindsorContainer())
            {
                container.Register(Component.For<IMessagingEngine>().Instance(MockRepository.GenerateMock<IMessagingEngine>()));
                container.AddFacility<CqrsFacility>(f => f.RunInMemory().Contexts(
                    Register.BoundedContext("bc1")
                        .PublishingEvents(typeof(int)).With("events1").WithLoopback()
                        .ListeningCommands(typeof(string)).On("commands1").WithLoopback()
                        .WithCommandsHandler<CommandHandler>(),
                    Register.BoundedContext("bc2")
                        .PublishingEvents(typeof(int)).With("events2").WithLoopback()
                        .ListeningCommands(typeof(string)).On("commands2").WithLoopback()
                        .WithCommandsHandler<CommandHandler>(),
                    Register.Saga<TestSaga>("SomeIntegration")
                        .ListeningEvents(typeof(int)).From("bc1").On("events1")
                        .ListeningEvents(typeof(int)).From("bc2").On("events2")
                        .PublishingCommands(typeof(string)).To("bc2").With("commands2"),
                    Register.DefaultRouting.PublishingCommands(typeof(string)).To("bc2").With("commands2")
                   ));

                container.Register(
                    Component.For<CommandHandler>(),
                    Component.For<TestSaga>()
                    );

                container.Resolve<ICqrsEngineBootstrapper>().Start();

                var cqrsEngine = (CqrsEngine)container.Resolve<ICqrsEngine>();
                cqrsEngine.SendCommand("cmd", "bc1", "bc1");

                Assert.That(TestSaga.Complete.WaitOne(1000), Is.True, "Saga has not got events or failed to send command");
            }
        }
        [Test]
        public void WithRepositoryAccessTest()
        {
            var log = MockRepository.GenerateMock<ILog>();
            using (var container = new WindsorContainer())
            {
                container
                    .Register(Component.For<IMessagingEngine>().Instance(MockRepository.GenerateMock<IMessagingEngine>()))
                    .AddFacility<CqrsFacility>(f => f.RunInMemory().Contexts(Register.BoundedContext("bc")
                         .WithNEventStore(dispatchCommits => Wireup.Init()
                                                                            .LogTo(type => log)
                                                                            .UsingInMemoryPersistence()
                                                                            .InitializeStorageEngine()
                                                                            .UsingJsonSerialization()
                                                                            .UsingSynchronousDispatchScheduler()
                                                                            .DispatchTo(dispatchCommits))))
                    .Register(Component.For<RepositoryDependentComponent>().WithRepositoryAccess("bc"))
                    .Resolve<ICqrsEngineBootstrapper>().Start();
                var cqrsEngine = (CqrsEngine)container.Resolve<ICqrsEngine>();
                var repositoryDependentComponent = container.Resolve<RepositoryDependentComponent>();
                Assert.That(repositoryDependentComponent.Repository, Is.Not.Null, "Repository was not injected");
            }
        }

    }


    public class TestSaga
    {
        public static List<string> Messages = new List<string>();
        public static ManualResetEvent Complete = new ManualResetEvent(false);
        private void Handle(int @event, ICommandSender sender, string boundedContext)
        {
            var message = string.Format("Event from {0} is caught by saga:{1}", boundedContext, @event);
            Messages.Add(message);
            if (boundedContext == "bc1")
                sender.SendCommand("cmd", "bc2");
            if (boundedContext == "bc2")
                Complete.Set();
            Console.WriteLine(message);
        }
    }


    public class EventListenerWithICommandSenderDependency
    {
        internal ICommandSender Sender { get; set; }

        public EventListenerWithICommandSenderDependency()
        {
        }

        void Handle(string m, ICommandSender sender)
        {
            Sender = sender;
        }
    }

    public class FakeEndpointResolver : IEndpointResolver
    {
        private readonly Dictionary<string, Endpoint> m_Endpoints = new Dictionary<string, Endpoint>
            {
                {"eventExchange", new Endpoint("test", "unistream.processing.events", true, "json")},
                {"eventQueue", new Endpoint("test", "unistream.processing.UPlusAdapter.TransferPublisher", true, "json")},
                {"commandExchange", new Endpoint("test", "unistream.u1.commands", true, "json")},
                {"commandQueue", new Endpoint("test", "unistream.u1.commands", true, "json")}
            };


        public Endpoint Resolve(string route, RoutingKey key, IEndpointProvider endpointProvider)
        {
            return m_Endpoints[route];
        }
    }

}