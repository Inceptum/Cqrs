namespace Inceptum.Cqrs.Configuration
{
    public static class LocalBoundedContext
    {


        public static LocalBoundedContextRegistration Named(string name)
        {
            return new LocalBoundedContextRegistration(name);
        }
    }
}