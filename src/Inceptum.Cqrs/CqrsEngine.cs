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
using Inceptum.Cqrs.Routing;
using Inceptum.Messaging.Configuration;
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


    public class DefaultEndpointProvider : IEndpointProvider
    {
        public bool Contains(string endpointName)
        {
            return false;
        }

        public Endpoint Get(string endpointName)
        {
            throw new ConfigurationErrorsException(string.Format("Endpoint '{0}' not found",endpointName));
        }
    }

    public class CqrsEngine :ICqrsEngine,IDisposable
    {
        readonly Logger m_Logger = LogManager.GetCurrentClassLogger();
        private readonly IMessagingEngine m_MessagingEngine;
        private readonly CompositeDisposable m_Subscription=new CompositeDisposable();
        internal  IEndpointResolver EndpointResolver { get; private set; }
     
        private readonly IEndpointProvider m_EndpointProvider;
        private readonly List<BoundedContext> m_BoundedContexts;
        internal List<BoundedContext> BoundedContexts {
            get { return m_BoundedContexts; }
        }
        private readonly IRegistration[] m_Registrations;
        private readonly IDependencyResolver m_DependencyResolver;
        private readonly bool m_CreateMissingEndpoints = false;

        protected IMessagingEngine MessagingEngine
        {
            get { return m_MessagingEngine; }
        }


        public CqrsEngine(IMessagingEngine messagingEngine, IEndpointResolver endpointResolver, IEndpointProvider endpointProvider, params IRegistration[] registrations)
            : this(new DefaultDependencyResolver(), messagingEngine,endpointResolver, endpointProvider, registrations)
        {
        }


        public CqrsEngine(IMessagingEngine messagingEngine, IEndpointResolver endpointResolver,params IRegistration[] registrations)
            : this(new DefaultDependencyResolver(), messagingEngine, endpointResolver, new DefaultEndpointProvider(), registrations)
        {
        }

        public CqrsEngine(IDependencyResolver dependencyResolver, IMessagingEngine messagingEngine, IEndpointResolver endpointResolver, IEndpointProvider endpointProvider, params IRegistration[] registrations)
            : this(dependencyResolver, messagingEngine, endpointResolver, endpointProvider, false, registrations)
        {
        }

        public CqrsEngine(IDependencyResolver dependencyResolver, IMessagingEngine messagingEngine, IEndpointResolver endpointResolver, IEndpointProvider endpointProvider, bool createMissingEndpoints, params IRegistration[] registrations)
        {
            m_Logger.Debug("CqrsEngine instanciating. createMissingEndpoints: "+ createMissingEndpoints);
            m_CreateMissingEndpoints = createMissingEndpoints;
            m_DependencyResolver = dependencyResolver;
            m_Registrations = registrations;
            EndpointResolver =endpointResolver?? new DefaultEndpointResolver();
            m_MessagingEngine = messagingEngine;
            m_EndpointProvider = endpointProvider;
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
                boundedContext.Processes.ForEach(p => p.Start(boundedContext, boundedContext.EventsPublisher));
            }


            ensureEndpoints();

            foreach (var boundedContext in BoundedContexts)
            {
                foreach (var route in boundedContext.Routes)
                {
                            
                    BoundedContext context = boundedContext;
                    var subscriptions = route.MessageRoutes
                        .Where(r => r.Key.CommunicationType == CommunicationType.Subscribe)
                        .Select(r => new
                        {
                            type = r.Key.MessageType,
                            priority = r.Key.Priority,
                            remoteBoundedContext=r.Key.RemoteBoundedContext,
                            endpoint = new Endpoint(r.Value.TransportId, "", r.Value.Destination.Subscribe, r.Value.SharedDestination, r.Value.SerializationFormat)
                        })
                        .GroupBy(x => Tuple.Create(x.endpoint, x.priority,x.remoteBoundedContext))
                        .Select(g => new
                        {
                            endpoint = g.Key.Item1,
                            priority = g.Key.Item2,
                            remoteBoundedContext =g.Key.Item3,
                            types = g.Select(x=>x.type).ToArray()
                        });


                    foreach (var subscription in subscriptions)
                    {
                        var routeName = route.Name;
                        var endpoint = subscription.endpoint;
                        var remoteBoundedContext = subscription.remoteBoundedContext;
                        CallbackDelegate<object> callback=null;
                        string messageTypeName=null;
                        switch (route.Type)
                        {
                            case RouteType.Events:
                                callback = (@event, acknowledge) => context.EventDispatcher.Dispacth(remoteBoundedContext,@event, acknowledge);
                                messageTypeName = "event";
                                break;
                            case RouteType.Commands:
                                callback =
                                    (command, acknowledge) =>
                                        context.CommandDispatcher.Dispatch(command, acknowledge, endpoint, routeName);
                                messageTypeName = "command";
                                break;
                        }
                        
                        m_Subscription.Add(m_MessagingEngine.Subscribe(
                            endpoint,
                            callback,
                            (type, acknowledge) =>
                            {
                                throw new InvalidOperationException(string.Format("Unknown {0} received: {1}",messageTypeName, type));
                                //acknowledge(0, true);
                            },
                            routeName,
                            (int)subscription.priority,
                            subscription.types));
                    }
                }
            }
        }

        private void ensureEndpoints()
        {
            var allEndpointsAreValid = true;
            var errorMessage=new StringBuilder("Some endpoints are not valid:").AppendLine();
            var log = new StringBuilder();
            log.Append("Endpoints verification").AppendLine();
           


            foreach (var boundedContext in BoundedContexts)
            {
                log.AppendFormat("Bounded context '{0}':",boundedContext.Name).AppendLine();

                boundedContext.ResolveRoutes(m_EndpointProvider);
                foreach (var route in boundedContext.Routes)
                {
                    log.AppendFormat("\t{0} route '{1}':",route.Type, route.Name).AppendLine();
                    foreach (var messageRoute in route.MessageRoutes)
                    {
                        string error;
                        var routingKey = messageRoute.Key;
                        var endpoint = messageRoute.Value;
                        var messageTypeName = route.Type.ToString().ToLower().TrimEnd('s');
                        bool result = true;

                        if (routingKey.CommunicationType == CommunicationType.Publish)
                        {
                            if (!m_MessagingEngine.VerifyEndpoint(endpoint, EndpointUsage.Publish, m_CreateMissingEndpoints, out error))
                            {
                                errorMessage.AppendFormat(
                                    "Route '{1}' within bounded context '{0}' for {2} type '{3}' has resolved endpoint {4} that is not properly configured for publishing: {5}.",
                                    boundedContext.Name, route.Name, messageTypeName, routingKey.MessageType.Name, endpoint, error).AppendLine();
                                result = false;
                            }

                            log.AppendFormat("\t\tPublishing  '{0}' to {1}\t{2}", routingKey.MessageType.Name, endpoint, result ? "OK" : "ERROR:" + error).AppendLine();
                        }

                        if (routingKey.CommunicationType == CommunicationType.Subscribe)
                        {
                            if (!m_MessagingEngine.VerifyEndpoint(endpoint, EndpointUsage.Subscribe, m_CreateMissingEndpoints, out error))
                            {
                                errorMessage.AppendFormat(
                                    "Route '{1}' within bounded context '{0}' for {2} type '{3}' has resolved endpoint {4} that is not properly configured for subscription: {5}.",
                                    boundedContext.Name, route.Name, messageTypeName, routingKey.MessageType.Name, endpoint, error).AppendLine();
                                result = false;
                            }

                            log.AppendFormat("\t\tSubscribing '{0}' on {1}\t{2}", routingKey.MessageType.Name, endpoint, result ? "OK" : "ERROR:" + error).AppendLine();
                        }
                        allEndpointsAreValid = allEndpointsAreValid && result;
                    }
                    
                }
            }

            if (!allEndpointsAreValid)
                throw new ConfigurationErrorsException(errorMessage.ToString());

            m_Logger.Debug(log);
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

        public void SendCommand<T>(T command, string boundedContext, string remoteBoundedContext, uint priority = 0)
        {
            if(!sendMessage(typeof(T),command,RouteType.Commands,boundedContext,priority,remoteBoundedContext))
                throw new InvalidOperationException(string.Format("bound context '{0}' does not support command '{1}' with priority {2}", boundedContext, typeof(T), priority));
        }

        private bool sendMessage(Type type, object message, RouteType routeType, string boundedContext, uint priority, string remoteBoundedContext=null)
        {
            var context = BoundedContexts.FirstOrDefault(bc => bc.Name == boundedContext);
            if (context == null)
                throw new ArgumentException(string.Format("bound context {0} not found", boundedContext), "boundedContext");
            var publishDirections = (from route in context.Routes
                                     from messageRoute in route.MessageRoutes
                                     where messageRoute.Key.MessageType == type && 
                                           messageRoute.Key.RouteType == routeType && 
                                           messageRoute.Key.Priority == priority &&
                                           messageRoute.Key.RemoteBoundedContext==remoteBoundedContext
                                     select new
                                     {
                                         routeName = route.Name,
                                         endpoint = messageRoute.Value
                                     }).ToArray();
            if (!publishDirections.Any())
                return false;
            foreach (var direction in publishDirections)
            {
                m_MessagingEngine.Send(message, direction.endpoint, direction.routeName);
            }
            return true;
        }

        public void ReplayEvents(string boundedContext, string remoteBoundedContext, params Type[] types)
        {
            var context = BoundedContexts.FirstOrDefault(bc => bc.Name == boundedContext);
                if (context == null)
                throw new ArgumentException(string.Format("bound context {0} not found",boundedContext),"boundedContext");
            var publishDirections = (from route in context.Routes
                                     from messageRoute in route.MessageRoutes
                                     where messageRoute.Key.MessageType == typeof(ReplayEventsCommand) && 
                                           messageRoute.Key.RouteType == RouteType.Commands && 
                                           messageRoute.Key.Priority == 0 &&
                                           messageRoute.Key.RemoteBoundedContext == remoteBoundedContext
                                     select new
                                     {
                                         routeName = route.Name,
                                         endpoint = messageRoute.Value

                                     }).ToArray();
            if(!publishDirections.Any())
                throw new InvalidOperationException(string.Format("bound context '{0}' does not support command '{1}' with priority {2}", remoteBoundedContext, typeof(ReplayEventsCommand), 0));

            var direction = publishDirections.First();
            var ep = direction.endpoint;
            Destination tmpDestination;
            if (context.GetTempDestination(ep.TransportId, () => m_MessagingEngine.CreateTemporaryDestination(ep.TransportId,"EventReplay"), out tmpDestination))
            {
                var replayEndpoint = new Endpoint { Destination = tmpDestination, SerializationFormat = ep.SerializationFormat, SharedDestination = true, TransportId = ep.TransportId };

                var knownEventTypes = (from route in context.Routes
                                       from messageRoute in route.MessageRoutes
                                       where  
                                               messageRoute.Key.RouteType == RouteType.Events &&
                                               messageRoute.Key.RemoteBoundedContext == remoteBoundedContext
                                       select  messageRoute.Key.MessageType).ToArray();
                
                
               // var knownEventTypes = context.EventsSubscriptions.SelectMany(e => e.Value).ToArray();
                m_Subscription.Add(m_MessagingEngine.Subscribe(
                    replayEndpoint,
                    (@event, acknowledge) => context.EventDispatcher.Dispacth(remoteBoundedContext,@event, acknowledge),
                    (typeName, acknowledge) => { }, 
                    "EventReplay",
                    0,
                    knownEventTypes));
            }
            SendCommand(new ReplayEventsCommand { Destination = tmpDestination.Publish, From = DateTime.MinValue, SerializationFormat = ep.SerializationFormat, Types = types }, boundedContext,remoteBoundedContext,0);
        }


        internal void PublishEvent(object @event, string boundedContext)
        {
            if (@event == null) throw new ArgumentNullException("event");
            if (!sendMessage(@event.GetType(), @event, RouteType.Events, boundedContext, 0))
                throw new InvalidOperationException(string.Format("bound context '{0}' does not support event '{1}'", boundedContext, @event.GetType()));
        }

        internal void PublishEvent(object @event, Endpoint endpoint, string processingGroup)
        {
            m_MessagingEngine.Send(@event, endpoint, processingGroup);
        }

        internal IDependencyResolver DependencyResolver {
            get { return m_DependencyResolver; }
        }

    }

    public interface ICqrsEngine
    {
        void ReplayEvents(string boundedContext, string remoteBoundedContext, params Type[] types);
        void SendCommand<T>(T command, string boundedContext, string remoteBoundedContext, uint priority = 0);
    }
}