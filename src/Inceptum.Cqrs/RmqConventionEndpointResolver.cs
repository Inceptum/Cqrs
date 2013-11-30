using System;
using System.Collections.Generic;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs
{
    public class RmqConventionEndpointResolver /*: IEndpointResolver*/
    {
        Dictionary<Tuple<string,string>,Endpoint> m_Cache=new  Dictionary<Tuple<string, string>, Endpoint>();
        
        public Endpoint Resolve(string boundedContext,string endpoint)
        {
            Endpoint ep;
            lock (m_Cache)
            {
                var key = Tuple.Create(boundedContext, endpoint);
                if (m_Cache.TryGetValue(key, out ep)) return ep;
                ep=createEndpoint(boundedContext, endpoint);
                m_Cache.Add(key,ep);
            }
            return ep;
        }

        private static Endpoint createEndpoint(string boundedContext, string endpoint)
        {
            return new Endpoint
            {
                Destination = new Destination
                {
                    Publish = string.Format("topic://{0}.Exchange/{1}", boundedContext, endpoint),
                    Subscribe = string.Format("{0}.Queue.{1}", boundedContext, endpoint)
                },
                SerializationFormat = "protobuf",
                SharedDestination = true,
                TransportId = "main"
            };
        }
    }
}