using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;
using System.Text;
using Castle.Components.DictionaryAdapter.Xml;
using Inceptum.Cqrs.Configuration;
using Inceptum.Cqrs.InfrastructureCommands;
using Inceptum.Messaging.Contract;
using NLog;

namespace Inceptum.Cqrs
{
    public interface IDependencyResolver
    {
        object GetService(Type type);
        bool HasService(Type type);
    }

    internal class DefaultDependencyResolver : IDependencyResolver
    {
        public object GetService(Type type)
        {
            return Activator.CreateInstance(type);
        }

        public bool HasService(Type type)
        {
            return !type.IsInterface;
        }
    }

    class MessageRoute
    {
        public MessageType MessageType { get; set; }
        public Type Type { get; set; }
        public Endpoint Endpoint { get; set; }

        public Endpoint EffectiveEndpoint
        {
            get
            {
                    return new Endpoint{
                        TransportId = Endpoint.TransportId,
                        Destination = Direction == EndpointUsage.Publish ? new Destination { Publish = Endpoint.Destination.Publish } : new Destination { Subscribe = Endpoint.Destination.Subscribe },
                        SerializationFormat = Endpoint.SerializationFormat,
                        SharedDestination = Endpoint.SharedDestination
                    };
            }
        }

        public string BoundedContext { get; set; }
        public string Route { get; set; }
        public EndpointUsage Direction { get; set; }
        public string ErrorMessage
        {
            get
            {
                return string.Format("Bounded context '{0}' route '{1}' is not properly configured for {4} {2} of type'{3}'", BoundedContext, Route, MessageType.ToString().ToLower(), Type,Direction==EndpointUsage.Publish?"publishing of":"subscription for");
            }
        }
    }

    public class CqrsEngine :ICqrsEngine
    {
        readonly Logger m_Logger = LogManager.GetCurrentClassLogger();
        private readonly IMessagingEngine m_MessagingEngine;
        private readonly CompositeDisposable m_Subscription=new CompositeDisposable();
        private readonly IEndpointResolver m_EndpointResolver;
        private readonly List<BoundedContext> m_BoundedContexts;
        internal List<BoundedContext> BoundedContexts {
            get { return m_BoundedContexts; }
        }
        private readonly IRegistration[] m_Registrations;
        private readonly IDependencyResolver m_DependencyResolver;
        private bool m_CreateMissingEndpoints = false;

        protected IMessagingEngine MessagingEngine
        {
            get { return m_MessagingEngine; }
        }


        public CqrsEngine(IMessagingEngine messagingEngine, IEndpointResolver endpointResolver, params IRegistration[] registrations)
            :this(new DefaultDependencyResolver(), messagingEngine, endpointResolver, registrations)
        {
        }

        public CqrsEngine(IDependencyResolver dependencyResolver, IMessagingEngine messagingEngine, IEndpointResolver endpointResolver,
            params IRegistration[] registrations)
        {
            m_DependencyResolver = dependencyResolver;
            m_Registrations = registrations;
            m_EndpointResolver = endpointResolver;
            m_MessagingEngine = messagingEngine;
            m_BoundedContexts=new List<BoundedContext>();
            init();
            
        }

        private void init()
        {
            foreach (var registration in m_Registrations)
            {
                registration.Create(this);
            }
            foreach (var registration in m_Registrations)
            {
                registration.Process(this);
            }

            foreach (var boundedContext in BoundedContexts)
            {
                boundedContext.Processes.ForEach(p => p.Start(this, boundedContext.EventsPublisher));
            }

            var messageRoutes = BoundedContexts.SelectMany(bc => bc.MessageRoutes).ToArray();
            var errorMessage=new StringBuilder();
            bool endpointsAreValid = true;
            foreach (var route in messageRoutes)
            {
                string error;
                route.Endpoint = m_EndpointResolver.Resolve(route.BoundedContext, route.Route, route.Type, route.MessageType);
                endpointsAreValid = m_MessagingEngine.VerifyEndpoint(route.Endpoint, EndpointUsage.Publish, m_CreateMissingEndpoints, out error)&& endpointsAreValid;
                errorMessage.AppendFormat("{0}: {1}", route.ErrorMessage, error);
            }

            if (!endpointsAreValid)
            {
                throw new ConfigurationErrorsException(errorMessage.ToString());
            }

            foreach (var boundedContext in m_BoundedContexts)
            {
                var context = boundedContext; 
                var subscribeEvents = boundedContext.MessageRoutes.Where(r => r.MessageType == MessageType.Event && r.Direction == EndpointUsage.Subscribe).GroupBy(r => r.EffectiveEndpoint);
                foreach (var subscribeEvent in subscribeEvents)
                {
                    var endpoint = subscribeEvent.Key;
                    m_Subscription.Add(m_MessagingEngine.Subscribe(
                        endpoint,
                        (@event, acknowledge) => context.EventDispatcher.Dispacth(@event, acknowledge),
                        (type, acknowledge) =>
                        {
                            m_Logger.Debug("Unknown event received: " + type);
                            acknowledge(0, true);
                        },
                        subscribeEvent.Select(r => r.Type).ToArray()));
                }
                var subscribeCommands = boundedContext.MessageRoutes.Where(r => r.MessageType == MessageType.Command && r.Direction == EndpointUsage.Subscribe).GroupBy(r => r.EffectiveEndpoint);
                foreach (var subscribeCommand in subscribeCommands)
                {
                    //TODO: handle priority
                    var endpoint = subscribeCommand.Key;
                    m_Subscription.Add(m_MessagingEngine.Subscribe(
                        endpoint,
                        (command, acknowledge) =>
                            context.CommandDispatcher.Dispatch(command, CommandPriority.Normal, acknowledge, endpoint),
                        (type, acknowledge) =>
                        {
                            m_Logger.Debug("Unknown command received: " + type);
                            acknowledge(0, true);
                        },
                        subscribeCommand.Select(r => r.Type).ToArray()));
                }
            }
        }
 

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var boundedContext in BoundedContexts.Where(b => b != null))
                {

                    if (boundedContext.Processes != null)
                    {
                        foreach (var process in boundedContext.Processes)
                        {
                            process.Dispose();
                        }
                    }

                    boundedContext.Dispose();

                }

                if (m_Subscription != null)
                    m_Subscription.Dispose();
            }
        }

        public void SendCommand<T>(T command,string boundedContext,CommandPriority priority=CommandPriority.Normal )
        {
            var context = BoundedContexts.FirstOrDefault(bc => bc.Name == boundedContext);
            if (context == null)
                throw new ArgumentException(string.Format("bound context {0} not found",boundedContext),"boundedContext");
            string endpoint;
/*
            if (!context.CommandRoutes.TryGetValue(Tuple.Create(typeof (T),priority), out endpoint))
            {
                throw new InvalidOperationException(string.Format("bound context '{0}' does not support command '{1}' with priority {2}",boundedContext,typeof(T),priority));
            }
            m_MessagingEngine.Send(command, m_EndpointResolver.Resolve(boundedContext, endpoint));
*/
            var route = context.MessageRoutes.FirstOrDefault(r => r.Type == typeof(T) && r.MessageType == MessageType.Command && r.Direction == EndpointUsage.Publish);
            if(route==null)
                throw new InvalidOperationException(string.Format("bound context '{0}' does not support command '{1}' with priority {2}", boundedContext, typeof(T), priority));
            m_MessagingEngine.Send(command,route.EffectiveEndpoint);
        }

        public void ReplayEvents(string boundedContext, params Type[] types)
        {
            var context = BoundedContexts.FirstOrDefault(bc => bc.Name == boundedContext);
                if (context == null)
                throw new ArgumentException(string.Format("bound context {0} not found",boundedContext),"boundedContext");


                var route = context.MessageRoutes.FirstOrDefault(r => r.Type == typeof(ReplayEventsCommand) && r.MessageType == MessageType.Command && r.Direction == EndpointUsage.Publish);
                if (route == null)
                    throw new InvalidOperationException(string.Format("bound context '{0}' does not support command '{1}' with priority {2}", boundedContext, typeof(ReplayEventsCommand), CommandPriority.Normal));
               
            Destination tmpDestination;
            var ep = route.EffectiveEndpoint;
            if (context.GetTempDestination(ep.TransportId, () => m_MessagingEngine.CreateTemporaryDestination(ep.TransportId,"EventReplay"), out tmpDestination))
            {
                var replayEndpoint = new Endpoint { Destination = tmpDestination, SerializationFormat = ep.SerializationFormat, SharedDestination = true, TransportId = ep.TransportId };
                var knownEventTypes = context.MessageRoutes.Where(r=>r.Direction==EndpointUsage.Subscribe && r.MessageType==MessageType.Event).Select( r=> r.Type).ToArray();
                m_Subscription.Add(m_MessagingEngine.Subscribe(
                    replayEndpoint,
                    (@event, acknowledge) => context.EventDispatcher.Dispacth(@event, acknowledge),
                    (typeName, acknowledge) => { }, 
                    "EventReplay",
                    knownEventTypes));
            }
            SendCommand(new ReplayEventsCommand { Destination = tmpDestination.Publish, From = DateTime.MinValue, SerializationFormat = ep.SerializationFormat, Types = types }, boundedContext);
        }

        internal void PublishEvent(object @event, BoundedContext boundedContext)
        {
            var route = boundedContext.MessageRoutes.FirstOrDefault(r => r.Type == @event.GetType() && r.MessageType == MessageType.Event && r.Direction == EndpointUsage.Publish);
            if (route == null)
                throw new InvalidOperationException(string.Format("bound context '{0}' does not support event '{1}'",boundedContext.Name, @event.GetType()));
            PublishEvent(@event, route.EffectiveEndpoint);
        }

        internal void PublishEvent(object @event,Endpoint endpoint)
        {
            m_MessagingEngine.Send(@event, endpoint);
        }

        internal IDependencyResolver DependencyResolver {
            get { return m_DependencyResolver; }
        }
    }

    internal interface ICqrsEngine : ICommandSender
    {

    }
}