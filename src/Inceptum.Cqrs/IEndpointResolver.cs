﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Inceptum.Cqrs.Routing;
using Inceptum.Messaging.Configuration;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs
{

    public enum RouteType
    {
        Commands,
        Events
    }
    public enum CommunicationType
    {
        Publish,
        Subscribe,
    }
    public interface IEndpointResolver
    {
        Endpoint Resolve(string route, RoutingKey key, IEndpointProvider endpointProvider);
    }

    public class DefaultEndpointResolver : IEndpointResolver
    {
        public Endpoint Resolve(string route, RoutingKey key, IEndpointProvider endpointProvider)
        {
            if (endpointProvider.Contains(route))
            {
                return endpointProvider.Get(route);
            }
            throw new ConfigurationErrorsException(string.Format("Endpoint '{0}' not found",route));
        }
    }


    public class MapEndpointResolver : IEndpointResolver
    {
        private IEndpointResolver m_FallbackResolver;
        private readonly Dictionary<Func<RoutingKey, bool>, string> m_Map;

        public MapEndpointResolver()
        {
            m_Map = new Dictionary<Func<RoutingKey, bool>, string>();
        }

        internal void SetFallbackResolver(IEndpointResolver fallbackResolver, bool replace=false)
        {
            if (replace||m_FallbackResolver==null)
                m_FallbackResolver = fallbackResolver;
        }

        public Endpoint Resolve(string route, RoutingKey key, IEndpointProvider endpointProvider)
        {
            var endpointName = m_Map.Where(pair => pair.Key(key)).Select(pair => pair.Value).SingleOrDefault();
            if(endpointName==null)
                return m_FallbackResolver.Resolve(route, key, endpointProvider);
            return endpointProvider.Get(endpointName);
        }

        public void AddSelector(Func<RoutingKey, bool> criteria, string endpoint)
        {
            m_Map.Add(criteria, endpoint);
        }
    }
}