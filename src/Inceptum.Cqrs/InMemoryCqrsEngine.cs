using System;
using System.Collections.Generic;
using Inceptum.Cqrs.Configuration;
using Inceptum.Messaging;
using Inceptum.Messaging.Configuration;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs
{



    public class InMemoryCqrsEngine : CqrsEngine
    {
        public InMemoryCqrsEngine(params IRegistration[] registrations) :
            base(new MessagingEngine(new TransportResolver(new Dictionary<string, TransportInfo> { { "InMemory", new TransportInfo("none", "none", "none", null, "InMemory") } })),
                new InMemoryEndpointResolver(), 
                registrations
            )
        {
             
        }
 

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                MessagingEngine.Dispose();
            }
        }
    }

  
}