using System;
using System.Collections.Generic;
using Inceptum.Messaging.Configuration;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs
{
    public class RabbitMqConventionEndpointResolver : IEndpointResolver
    {
        readonly Dictionary<Tuple<string, string, string, Type, RouteType>, Endpoint> m_Cache = new Dictionary<Tuple<string, string, string, Type, RouteType>, Endpoint>();
        private readonly IEndpointProvider m_EndpointProvider;
        private readonly string m_Transport;
        private string m_SerializationFormat;

        public RabbitMqConventionEndpointResolver(string transport,string serializationFormat,IEndpointProvider endpointProvider)
        {
            m_Transport = transport;
            m_EndpointProvider = endpointProvider;
            m_SerializationFormat = serializationFormat;
        }


        public Endpoint Resolve(string localBoundedContext, string remoteBoundedContext, string endpoint, Type type, RouteType routeType)
        {
            lock (m_Cache)
            {
                var key = Tuple.Create(localBoundedContext,remoteBoundedContext, endpoint,type,routeType );
                Endpoint ep;
                if (m_Cache.TryGetValue(key, out ep)) return ep;

                if (m_EndpointProvider.Contains(endpoint))
                {
                    ep = m_EndpointProvider.Get(endpoint);
                    m_Cache.Add(key, ep);
                    return ep;
                }

                ep=createEndpoint(localBoundedContext, remoteBoundedContext,  endpoint,type,routeType);
                m_Cache.Add(key,ep);
                return ep;
            }
        }

        private Endpoint createEndpoint(string localBoundedContext, string remoteBoundedContext, string endpoint, Type type,RouteType routeType)
        {
            m_SerializationFormat = "protobuf";
            string subscribe;
            if (routeType == RouteType.Commands)
            {
                subscribe = remoteBoundedContext == localBoundedContext
                    ? string.Format("{0}.queue.{1}.{2}", localBoundedContext, routeType.ToString().ToLower(), endpoint)
                    : null;
            }
            else
            {
                subscribe = string.Format("{0}.queue.{1}.{2}.{3}", localBoundedContext, remoteBoundedContext , routeType.ToString().ToLower(), endpoint);
            }
            
            return new Endpoint
            {
                Destination = new Destination
                {
                    Publish = string.Format("topic://{0}.{1}.exchange/{2}", remoteBoundedContext, routeType.ToString().ToLower(), type.Name),
                    Subscribe = subscribe
                },
                SerializationFormat = m_SerializationFormat,
                SharedDestination = true,
                TransportId = m_Transport
            };
        }
    }

   
}