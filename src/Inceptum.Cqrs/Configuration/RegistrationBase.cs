using System;
using System.Collections.Generic;
using System.Linq;

namespace Inceptum.Cqrs.Configuration
{
    public abstract class RegistrationBase<TRegistration,TSubject> : IRegistration where TRegistration : class, IRegistration
    {
        private Type[] m_Dependencies = new Type[0];
        private readonly List<IDescriptor<TSubject>> m_Descriptors = new List<IDescriptor<TSubject>>();
        private TSubject m_Subject;

        IEnumerable<Type> IRegistration.Dependencies
        {
            get { return m_Dependencies; }
        }

        void IRegistration.Create(CqrsEngine cqrsEngine)
        {
            m_Subject = GetSubject(cqrsEngine);
            foreach (var descriptor in m_Descriptors)
            {
                descriptor.Create(m_Subject, cqrsEngine.DependencyResolver);
            }
        }

        protected abstract TSubject GetSubject(CqrsEngine cqrsEngine);


        void IRegistration.Process(CqrsEngine cqrsEngine)
        {
            foreach (var descriptor in m_Descriptors)
            {
                descriptor.Process(m_Subject, cqrsEngine);
            }
        }

        protected T AddDescriptor<T>(T descriptor) where T : IDescriptor<TSubject>
        {
            m_Dependencies = m_Dependencies.Concat(descriptor.GetDependencies()).Distinct().ToArray();
            m_Descriptors.Add(descriptor);
            return descriptor;
        }

    }
}