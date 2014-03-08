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
        private readonly IEndpointProvider m_EndpointProvider;
        private readonly string m_Transport;
        private string m_SerializationFormat;

        public RabbitMqConventionEndpointResolver(string transport,string serializationFormat,IEndpointProvider endpointProvider)
        {
            m_Transport = transport;
            m_EndpointProvider = endpointProvider;
            m_SerializationFormat = serializationFormat;
        }



        private Endpoint createEndpoint(string route, RoutingKey key)
        {
            m_SerializationFormat = "protobuf";
            string subscribe;
            if (key.RouteType == RouteType.Commands)
            {
                subscribe = key.RemoteBoundedContext == key.LocalBoundedContext 
                    ? string.Format("{0}.queue.{1}.{2}", key.LocalBoundedContext, key.CommunicationType.ToString().ToLower(), route)
                    : null;
            }
            else
            {
                subscribe = string.Format("{0}.queue.{1}.{2}.{3}", key.LocalBoundedContext, key.RemoteBoundedContext, key.CommunicationType.ToString().ToLower(), route);
            }

            return new Endpoint
            {
                Destination = new Destination
                {
                    Publish = string.Format("topic://{0}.{1}.exchange/{2}", key.RemoteBoundedContext, key.CommunicationType.ToString().ToLower(), key.MessageType.Name),
                    Subscribe = subscribe
                },
                SerializationFormat = m_SerializationFormat,
                SharedDestination = true,
                TransportId = m_Transport
            };
        }
        public Endpoint Resolve(string route, RoutingKey key, IEndpointProvider endpointProvider)
        {
            lock (m_Cache)
            {
               
                Endpoint ep;
                if (m_Cache.TryGetValue(Tuple.Create(route,key), out ep)) return ep;

                if (m_EndpointProvider.Contains(route))
                {
                    ep = m_EndpointProvider.Get(route);
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