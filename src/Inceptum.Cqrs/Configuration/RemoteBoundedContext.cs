namespace Inceptum.Cqrs.Configuration
{
    public static class RemoteBoundedContext
    {
        public static RemoteBoundedContextRegistration Named(string name,string localBoundedContext)
        {
            return new RemoteBoundedContextRegistration(name, localBoundedContext);
        }
    }
}