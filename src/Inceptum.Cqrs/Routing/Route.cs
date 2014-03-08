using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Globalization;
using System.Linq;
using Inceptum.Messaging.Configuration;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs.Routing
{
    public class RoutingKey
    {
        private readonly Dictionary<string, string> m_Hints = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        public Type MessageType { get; set; }
        public CommunicationType CommunicationType { get; set; }
        public RouteType RouteType { get; set; }
        public uint Priority { get; set; }
        public string LocalBoundedContext { get; set; }
        public string RemoteBoundedContext { get; set; }

        protected bool Equals(RoutingKey other)
        {
            return MessageType == other.MessageType &&
                   CommunicationType == other.CommunicationType &&
                   RouteType == other.RouteType &&
                   Priority == other.Priority &&
                   string.Equals(RemoteBoundedContext, other.RemoteBoundedContext) &&
                   string.Equals(LocalBoundedContext, other.LocalBoundedContext) &&
                   m_Hints.Keys.Count == other.m_Hints.Keys.Count &&
                   m_Hints.Keys.All(k => other.m_Hints.ContainsKey(k) && Equals(m_Hints[k], other.m_Hints[k]));
        }

        public string this[string key]
        {
            get
            {
                if (StringComparer.InvariantCultureIgnoreCase.Compare(key, "priority") == 0)
                    return Priority.ToString(CultureInfo.InvariantCulture);
                if (StringComparer.InvariantCultureIgnoreCase.Compare(key, "MessageType") == 0)
                    return MessageType.ToString();
                if (StringComparer.InvariantCultureIgnoreCase.Compare(key, "CommunicationType") == 0)
                    return CommunicationType.ToString();
                if (StringComparer.InvariantCultureIgnoreCase.Compare(key, "RouteType") == 0)
                    return RouteType.ToString();
                if (StringComparer.InvariantCultureIgnoreCase.Compare(key, "RemoteBoundContext") == 0)
                    return RemoteBoundedContext;
                if (StringComparer.InvariantCultureIgnoreCase.Compare(key, "LocalBoundedContext") == 0)
                    return LocalBoundedContext;
                string value;
                m_Hints.TryGetValue(key, out value);
                return value;
            }
            set
            {
                if (StringComparer.InvariantCultureIgnoreCase.Compare(key, "priority") == 0 ||
                StringComparer.InvariantCultureIgnoreCase.Compare(key, "MessageType") == 0||
                StringComparer.InvariantCultureIgnoreCase.Compare(key, "CommunicationType") == 0 ||
                StringComparer.InvariantCultureIgnoreCase.Compare(key, "RouteType") == 0 ||
                StringComparer.InvariantCultureIgnoreCase.Compare(key, "RemoteBoundContext") == 0||
                StringComparer.InvariantCultureIgnoreCase.Compare(key, "LocalBoundedContext") == 0)
                    throw new ArgumentException(key + " should be set with corresponding RoutingKey property", "key");
                m_Hints[key] = value;
            }
        }
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((RoutingKey) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (MessageType != null ? MessageType.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ CommunicationType.GetHashCode();
                hashCode = (hashCode * 397) ^ RouteType.GetHashCode();
                hashCode = (hashCode*397) ^ (int) Priority;
                hashCode = (hashCode * 397) ^ (LocalBoundedContext != null ? LocalBoundedContext.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (RemoteBoundedContext != null ? RemoteBoundedContext.GetHashCode() : 0);
                hashCode = m_Hints.Keys.OrderBy(k=>k).Aggregate(hashCode, (h, key) => (h * 397) ^ key.GetHashCode());
                hashCode = m_Hints.Values.OrderBy(v => v).Aggregate(hashCode, (h, value) => (h * 397) ^ (value!=null?value.GetHashCode():0));
                return hashCode;
            }
        }

        public static bool operator ==(RoutingKey left, RoutingKey right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(RoutingKey left, RoutingKey right)
        {
            return !Equals(left, right);
        }
    }

    public class Route 
    {
        private readonly Dictionary<RoutingKey,Endpoint> m_MessageRoutes =new Dictionary<RoutingKey, Endpoint>();
        private readonly Dictionary<RoutingKey, IEndpointResolver> m_RouteResolvers = new Dictionary<RoutingKey, IEndpointResolver>();
        private readonly string m_BoundedContext;
        public string Name { get; set; }
        public RouteType? Type { get; set; }
        public uint ConcurrencyLevel { get; set; }

        public Route(string name, string boundedContext)
        {
            m_BoundedContext = boundedContext;
            Name = name;
        }

        public IDictionary<RoutingKey, Endpoint> MessageRoutes
        {
            get { return new ReadOnlyDictionary<RoutingKey, Endpoint>(m_MessageRoutes); }
        }

        public void AddPublishedCommand(Type command, uint priority,string boundedContext, IEndpointResolver resolver)
        {
            if(Type==null)
                Type=RouteType.Commands;
            if(Type!=RouteType.Commands)
                throw new ConfigurationErrorsException(string.Format("Can not publish commands with events route '{0}'.",Name));
            var routingKey = new RoutingKey
            {
                LocalBoundedContext = m_BoundedContext,
                MessageType = command,
                Priority = priority,
                RouteType = Type.Value,
                CommunicationType = CommunicationType.Publish,
                RemoteBoundedContext = boundedContext
            };
            m_RouteResolvers[routingKey] = resolver;
        }

        public void AddSubscribedCommand(Type command, uint priority, IEndpointResolver resolver)
        {
            if (Type == null)
                Type = RouteType.Commands;
            if (Type != RouteType.Commands)
                throw new ConfigurationErrorsException(string.Format("Can not subscribe for commands on events route '{0}'.", Name));

            var routingKey = new RoutingKey
            {
                LocalBoundedContext = m_BoundedContext,
                MessageType = command,
                Priority = priority,
                RouteType = Type.Value,
                CommunicationType = CommunicationType.Subscribe
            };
            m_RouteResolvers[routingKey] = resolver;
        }


        public void AddPublishedEvent(Type @event, uint priority, IEndpointResolver resolver)
        {
            if (Type == null)
                Type = RouteType.Events;
            if (Type != RouteType.Events)
                throw new ConfigurationErrorsException(string.Format("Can not publish for events with commands route '{0}'.", Name));


            var routingKey = new RoutingKey
            {
                LocalBoundedContext = m_BoundedContext,
                RouteType = Type.Value,
                MessageType = @event,
                Priority = priority,
                CommunicationType = CommunicationType.Publish 
            };
            m_RouteResolvers[routingKey] = resolver;
        }

        public void AddSubscribedEvent(Type @event, uint priority,string boundedContext, IEndpointResolver resolver)
        {
            if (Type == null)
                Type = RouteType.Events;
            if (Type != RouteType.Events)
                throw new ConfigurationErrorsException(string.Format("Can not subscribe for events on commands route '{0}'.", Name));

            var routingKey = new RoutingKey
            {
                LocalBoundedContext = m_BoundedContext,
                RouteType = Type.Value,
                MessageType = @event,
                RemoteBoundedContext = boundedContext,
                Priority = priority,
                CommunicationType = CommunicationType.Subscribe 
            };
            m_RouteResolvers[routingKey] = resolver;
        }

        public Endpoint this[RoutingKey key]
        {
            get { return m_MessageRoutes[key]; }
        }

        public void Resolve(IEndpointProvider endpointProvider)
        {
            foreach (var pair in m_RouteResolvers)
            {
                var endpoint = pair.Value.Resolve(Name, pair.Key, endpointProvider);
                if (pair.Key.CommunicationType == CommunicationType.Subscribe)
                    endpoint = new Endpoint(endpoint.TransportId, "", endpoint.Destination.Subscribe, endpoint.SharedDestination, endpoint.SerializationFormat);
                m_MessageRoutes[pair.Key] = endpoint;
            }
        }
 
    }

   
}