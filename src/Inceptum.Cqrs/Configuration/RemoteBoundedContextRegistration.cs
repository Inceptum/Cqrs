using System;
using Inceptum.Cqrs.InfrastructureCommands;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs.Configuration
{
    public class RemoteBoundedContextRegistration : BoundedContextRegistration 
    {
        public RemoteBoundedContextRegistration(string name) : base(name)
        {
        }

        public RemoteListeningCommandsDescriptor ListeningCommands(params Type[] types)
        {
            return new RemoteListeningCommandsDescriptor(types, this);
        }

        public RemoteListeningCommandsDescriptor ListeningInfrastructureCommands()
        {
            return new RemoteListeningCommandsDescriptor(new[]{typeof(ReplayEventsCommand)}, this);
        }

        public RemotePublishingEventsDescriptor PublishingEvents(params Type[] types)
        {
            return new RemotePublishingEventsDescriptor(types, this);
        }
    }



    public class RemoteListeningCommandsDescriptor  
    {
        private readonly RemoteBoundedContextRegistration m_Registration;
        private readonly Type[] m_Types;
        
        public RemoteListeningCommandsDescriptor(Type[] types, RemoteBoundedContextRegistration registration) 
        {
            m_Types = types;
            m_Registration = registration;
        }

        public RemoteBoundedContextRegistration On(string publishEndpoint, CommandPriority priority = CommandPriority.Normal)
        {
            m_Registration.AddCommandsRoute(m_Types, publishEndpoint, priority);
            return m_Registration;
        }

    }

    public class RemotePublishingEventsDescriptor
    {
        private readonly Type[] m_Types;
        private readonly RemoteBoundedContextRegistration m_Registration;

        public RemotePublishingEventsDescriptor(Type[] types, RemoteBoundedContextRegistration registration)
        {
            m_Registration = registration;
            m_Types = types;
        }

        public RemoteBoundedContextRegistration To(string listenEndpoint)
        {
            m_Registration.AddSubscribedEvents(m_Types, listenEndpoint);
            return m_Registration;
        }
    }
}