namespace Inceptum.Cqrs.Configuration
{
    public static class RemoteBoundedContext
    {
        public static RemoteBoundedContextRegistration Named(string name)
        {
            return new RemoteBoundedContextRegistration(name);
        }
    }
}