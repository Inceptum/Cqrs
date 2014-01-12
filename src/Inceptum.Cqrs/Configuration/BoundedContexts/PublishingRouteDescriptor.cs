using System;
using System.Collections.Generic;

namespace Inceptum.Cqrs.Configuration
{
    public abstract class PublishingRouteDescriptor<T> : RouteDescriptorBase<T>, IPublishingRouteDescriptor<T> where T : RouteDescriptorBase
    {
        protected T Descriptor { private get; set; }
        protected string Route { get; private set; }

        protected PublishingRouteDescriptor(BoundedContextRegistration1 registration):base(registration)
        {
        }

        T IPublishingRouteDescriptor<T>.With(string route)
        {
            Route = route;
            return Descriptor;
        }

        public abstract IEnumerable<Type> GetDependencies();
        public abstract void Create(BoundedContext boundedContext, IDependencyResolver resolver);
        public abstract void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine);

    }
}