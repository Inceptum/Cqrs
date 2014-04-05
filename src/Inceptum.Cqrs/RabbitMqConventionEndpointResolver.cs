using System;
using System.Collections.Generic;
using Inceptum.Cqrs.Routing;
using Inceptum.Messaging.Configuration;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs
{
    public class RabbitMqConventionEndpointResolver : IEndpointResolver
    {
        readonly Dictionary<Tuple<string, RoutingKey>, Endpoint> m_Cache = new Dictionary<Tuple<string, RoutingKey>, Endpoint>();
        private readonly string m_Transport;
        private string m_SerializationFormat;
        private readonly string m_ExclusiveQueuePostfix;
        private readonly string m_EnvironmentPrefix;
        private static string m_CommandsKeyword;
        private static string m_EventsKeyword;

        public RabbitMqConventionEndpointResolver(string transport, string serializationFormat, string exclusiveQueuePostfix = null, string environment = null, string commandsKeyword = null, string eventsKeyword=null)
        {
            m_EnvironmentPrefix = environment!=null?environment+".":"";
            m_ExclusiveQueuePostfix = "." + (exclusiveQueuePostfix ?? Environment.MachineName);
            m_Transport = transport;
            m_SerializationFormat = serializationFormat;
            m_CommandsKeyword = commandsKeyword;
            m_EventsKeyword = eventsKeyword;
        }

        private string createQueueName(string queue,bool exclusive)
        {
            return string.Format("{0}{1}{2}", m_EnvironmentPrefix, queue, exclusive ? m_ExclusiveQueuePostfix : "");
        }
       private string createExchangeName(string exchange)
        {
            return string.Format("topic://{0}{1}", m_EnvironmentPrefix, exchange);
        }


        private Endpoint createEndpoint(string route, RoutingKey key)
        {
            m_SerializationFormat = "protobuf";
            var rmqRoutingKey = key.Priority == 0 ? key.MessageType.Name : key.MessageType.Name + "." + key.Priority;
            var queueName = key.Priority == 0 ? route : route + "." + key.Priority;
            if (key.RouteType == RouteType.Commands && key.CommunicationType == CommunicationType.Subscribe)
            {
                return new Endpoint
                {
                    Destination = new Destination
                    {
                        Publish = createExchangeName(string.Format("{0}.{1}.exchange/{2}", key.LocalContext, getKewordByRoutType(key.RouteType), rmqRoutingKey)),
                        Subscribe = createQueueName(string.Format("{0}.queue.{1}.{2}", key.LocalContext, getKewordByRoutType(key.RouteType), queueName), key.Exclusive)
                    },
                    SerializationFormat = m_SerializationFormat,
                    SharedDestination = true,
                    TransportId = m_Transport
                };
            }


            if (key.RouteType == RouteType.Commands && key.CommunicationType == CommunicationType.Publish)
            {
                return new Endpoint
                {
                    Destination = new Destination
                    {
                        Publish = createExchangeName(string.Format("{0}.{1}.exchange/{2}", key.RemoteBoundedContext, getKewordByRoutType(key.RouteType), rmqRoutingKey)),
                        Subscribe = null
                    },
                    SerializationFormat = m_SerializationFormat,
                    SharedDestination = true,
                    TransportId = m_Transport
                };
            }

            if (key.RouteType == RouteType.Events && key.CommunicationType == CommunicationType.Subscribe)
            {
                return new Endpoint
                {
                    Destination = new Destination
                    {
                        Publish = createExchangeName(string.Format("{0}.{1}.exchange/{2}", key.RemoteBoundedContext, getKewordByRoutType(key.RouteType), key.MessageType.Name)),
                        Subscribe = createQueueName(string.Format("{0}.queue.{1}.{2}.{3}", key.LocalContext, key.RemoteBoundedContext, getKewordByRoutType(key.RouteType), route), key.Exclusive)
                    },
                    SerializationFormat = m_SerializationFormat,
                    SharedDestination = true,
                    TransportId = m_Transport
                };
            }


            if (key.RouteType == RouteType.Events && key.CommunicationType == CommunicationType.Publish)
            {
                return new Endpoint
                {
                    Destination = new Destination
                    {
                        Publish = createExchangeName(string.Format("{0}.{1}.exchange/{2}", key.LocalContext, getKewordByRoutType(key.RouteType), key.MessageType.Name)),
                        Subscribe = null
                    },
                    SerializationFormat = m_SerializationFormat,
                    SharedDestination = true,
                    TransportId = m_Transport
                };
            }
            return default(Endpoint);

        }

        private static string getKewordByRoutType(RouteType routeType)
        {
            string keyword = null;
            switch (routeType)
            {
                    case RouteType.Commands:
                        keyword=m_CommandsKeyword;
                        break;
                    case RouteType.Events:
                        keyword=m_EventsKeyword;
                    break;
            }
            return keyword??(routeType.ToString().ToLower());
        }

        public Endpoint Resolve(string route, RoutingKey key, IEndpointProvider endpointProvider)
        {
            lock (m_Cache)
            {
               
                Endpoint ep;
                if (m_Cache.TryGetValue(Tuple.Create(route,key), out ep)) return ep;

                if (endpointProvider.Contains(route))
                {
                    ep = endpointProvider.Get(route);
                    m_Cache.Add(Tuple.Create(route, key), ep);
                    return ep;
                }

                ep = createEndpoint(route,key);
                m_Cache.Add(Tuple.Create(route, key), ep);
                return ep;
            }
        }

    }

   
}