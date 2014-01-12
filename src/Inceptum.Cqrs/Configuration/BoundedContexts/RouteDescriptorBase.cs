namespace Inceptum.Cqrs.Configuration
{
    public abstract class RouteDescriptorBase<T> : RouteDescriptorBase where T :  RouteDescriptorBase
    {

        protected RouteDescriptorBase(BoundedContextRegistration1 registration) : base(registration)
        {
        }

        public T Prioritized(uint lowestPriority)
        {
            LowestPriority = lowestPriority;
            return this as T;
        }


        public ExplicitEndpointDescriptor<T> WithEndpoint(string endpoint)
        {
            return new ExplicitEndpointDescriptor<T>(endpoint, this as T);
        }
    }
}