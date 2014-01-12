using System;
using System.Collections.Generic;
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
        public RouteType RouteType { get; set; }
        public uint Priority { get; set; }
        public string LocalBoundedContext { get; set; }
        public string RemoteBoundContext { get; set; }

        protected bool Equals(RoutingKey other)
        {
            return MessageType == other.MessageType &&
                   RouteType == other.RouteType &&
                   Priority == other.Priority &&
                   string.Equals(RemoteBoundContext, other.RemoteBoundContext) &&
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
                if (StringComparer.InvariantCultureIgnoreCase.Compare(key, "RouteType") == 0)
                    return RouteType.ToString();
                if (StringComparer.InvariantCultureIgnoreCase.Compare(key, "RemoteBoundContext") == 0)
                    return RemoteBoundContext;
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
                hashCode = (hashCode*397) ^ RouteType.GetHashCode();
                hashCode = (hashCode*397) ^ (int) Priority;
                hashCode = (hashCode * 397) ^ (LocalBoundedContext != null ? LocalBoundedContext.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (RemoteBoundContext != null ? RemoteBoundContext.GetHashCode() : 0);
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

    public class RouteMap 
    {
        private readonly Dictionary<RoutingKey,Endpoint> m_Routes =new Dictionary<RoutingKey, Endpoint>();
        private readonly Dictionary<RoutingKey, IEndpointResolver> m_RouteResolvers = new Dictionary<RoutingKey, IEndpointResolver>();
        public string Name { get; set; }
        public RouteType Type { get; set; }
        public uint ConcurrencyLevel { get; set; }

        public void AddRoute(RoutingKey routingKey, IEndpointResolver resolver)
        {
            m_RouteResolvers[routingKey] = resolver;
        }

        public Endpoint this[RoutingKey key]
        {
            get { return m_Routes[key]; }
        }

        public void Resolve(IEndpointProvider endpointProvider)
        {
            foreach (var pair in m_RouteResolvers)
            {
                m_Routes[pair.Key] = pair.Value.Resolve(Name, pair.Key, endpointProvider);
            }
        }
    }

    class MyClass
    {
        private RouteMap PublishCommands;
        private RouteMap SubscribeCommands;
        private RouteMap PublishEvents;
        private RouteMap SubscribeEvents;
    }
}