﻿using System;
using System.Collections.Generic;
using Inceptum.Messaging.Configuration;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs
{
    public class RabbitMqConventionEndpointResolver : IEndpointResolver
    {
        readonly Dictionary<Tuple<string,string>,Endpoint> m_Cache=new  Dictionary<Tuple<string, string>, Endpoint>();
        private readonly IEndpointProvider m_EndpointProvider;
        private readonly string m_Transport;
        private string m_SerializationFormat;

        public RabbitMqConventionEndpointResolver(string transport,string serializationFormat,IEndpointProvider endpointProvider)
        {
            m_Transport = transport;
            m_EndpointProvider = endpointProvider;
            m_SerializationFormat = serializationFormat;
        }


        public Endpoint Resolve(string localBoundedContext, string remoteBoundedContext, string endpoint, Type type)
        {
            lock (m_Cache)
            {
                var key = Tuple.Create(localBoundedContext, endpoint);
                Endpoint ep;
                if (m_Cache.TryGetValue(key, out ep)) return ep;

                if (m_EndpointProvider.Contains(endpoint))
                {
                    ep = m_EndpointProvider.Get(endpoint);
                    m_Cache.Add(key, ep);
                    return ep;
                }

                ep=createEndpoint(localBoundedContext, remoteBoundedContext,  endpoint,type);
                m_Cache.Add(key,ep);
                return ep;
            }
        }

        private Endpoint createEndpoint(string localBoundedContext, string remoteBoundedContext, string endpoint, Type type)
        {
            m_SerializationFormat = "protobuf";
            return new Endpoint
            {
                Destination = new Destination
                {
                    Publish = string.Format("topic://{0}.E/{1}", localBoundedContext, type.Name),
                    Subscribe = string.Format("{0}.Q.{1}.{2}", localBoundedContext, remoteBoundedContext, endpoint)
                },
                SerializationFormat = m_SerializationFormat,
                SharedDestination = true,
                TransportId = m_Transport
            };
        }
    }
}