using System;
using System.Collections.Generic;

namespace Inceptum.Cqrs.Configuration
{
    public abstract class BoundedContextRegistrationWrapper : IBoundedContextRegistration
    {
        private readonly BoundedContextRegistration1 m_Registration;

        protected BoundedContextRegistrationWrapper(BoundedContextRegistration1 registration)
        {
            m_Registration = registration;
        }
        public string BoundedContextName
        {
            get { return m_Registration.BoundedContextName; }
        }

        public SendingCommandsDescriptor SendingCommands(params Type[] commandsTypes)
        {
            return m_Registration.SendingCommands(commandsTypes);
        }

        public ListeningEventsDescriptor ListeningEvents(params Type[] types)
        {
            return m_Registration.ListeningEvents(types);
        }

        public IListeningRouteDescriptor<ListeningCommandsDescriptor> ListeningCommands(params Type[] types)
        {
            return m_Registration.ListeningCommands(types);
        }

        public IPublishingRouteDescriptor<PublishingEventsDescriptor> PublishingEvents(params Type[] types)
        {
            return m_Registration.PublishingEvents(types);
        }

        public ProcessingOptionsDescriptor ProcessingOptions(string route)
        {
            return m_Registration.ProcessingOptions(route);
        }

        void IRegistration.Create(CqrsEngine cqrsEngine)
        {
            (m_Registration as IRegistration).Create(cqrsEngine);
        }

        void IRegistration.Process(CqrsEngine cqrsEngine)
        {
            (m_Registration as IRegistration).Process(cqrsEngine);
        }

        IEnumerable<Type> IRegistration.Dependencies
        {
            get { return (m_Registration as IRegistration).Dependencies; }
        }
    }
}