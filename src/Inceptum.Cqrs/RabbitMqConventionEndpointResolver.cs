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

        public RabbitMqConventionEndpointResolver(string transport,string serializationFormat)
        {
            m_Transport = transport;
            m_SerializationFormat = serializationFormat;
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
                        Publish = string.Format("topic://{0}.{1}.exchange/{2}", key.LocalContext, key.RouteType.ToString().ToLower(), rmqRoutingKey),
                        Subscribe = string.Format("{0}.queue.{1}.{2}", key.LocalContext, key.RouteType.ToString().ToLower(), queueName)
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
                        Publish = string.Format("topic://{0}.{1}.exchange/{2}", key.RemoteBoundedContext, key.RouteType.ToString().ToLower(), rmqRoutingKey),
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
                        Publish = string.Format("topic://{0}.{1}.exchange/{2}", key.RemoteBoundedContext, key.RouteType.ToString().ToLower(), key.MessageType.Name),
                        Subscribe =  string.Format("{0}.queue.{1}.{2}.{3}", key.LocalContext, key.RemoteBoundedContext, key.RouteType.ToString().ToLower(), route)
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
                        Publish = string.Format("topic://{0}.{1}.exchange/{2}", key.LocalContext, key.RouteType.ToString().ToLower(), key.MessageType.Name),
                        Subscribe = null
                    },
                    SerializationFormat = m_SerializationFormat,
                    SharedDestination = true,
                    TransportId = m_Transport
                };
            }
            return default(Endpoint);

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