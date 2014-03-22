using Inceptum.Cqrs.Configuration.BoundedContext;
using Inceptum.Cqrs.Configuration.Routing;
using Inceptum.Cqrs.Configuration.Saga;

namespace Inceptum.Cqrs.Configuration
{
    public static class Register
    {
        public static ISagaRegistration Saga<TSaga>(string name)
        {
            return new SagaRegistration(name,typeof(TSaga));
        }

        public static IBoundedContextRegistration BoundedContext(string name)
        {
            return new BoundedContextRegistration(name);
        }
        
        public static IDefaultRoutingRegistration DefaultRouting
        {
            get { return new DefaultRoutingRegistration(); }
        }

        public static DefaultEndpointResolverRegistration DefaultEndpointResolver(IEndpointResolver resolver)
        {
            return new DefaultEndpointResolverRegistration(resolver);
        }
        public static DefaultEndpointResolverRegistration DefaultEndpointResolver<TResolver>() 
            where TResolver: IEndpointResolver 
        {
            return new DefaultEndpointResolverRegistration(typeof(TResolver));
        }
    }
}