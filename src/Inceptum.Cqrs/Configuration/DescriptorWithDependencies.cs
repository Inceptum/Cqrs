using System;
using System.Collections.Generic;
using System.Linq;

namespace Inceptum.Cqrs.Configuration
{
    abstract class DescriptorWithDependencies<TSubject> : IDescriptor<TSubject>
    {
        private readonly Type[] m_Dependedncies = new Type[0];
        private readonly Func<Func<Type, object>, IEnumerable<object>> m_ResolveDependedncies;

        protected DescriptorWithDependencies(params object[] dependencies)
        {
            m_ResolveDependedncies = r => dependencies;
        }

        protected DescriptorWithDependencies(params Type[] dependencies)
        {
            m_Dependedncies = dependencies;
            m_ResolveDependedncies = dependencies.Select;

        }

        public IEnumerable<Type> GetDependencies()
        {
            return m_Dependedncies;
        }

        public void Create(TSubject subject, IDependencyResolver resolver)
        {
           
            ResolvedDependencies = m_ResolveDependedncies(resolver.GetService);
            Create(subject);
        }

        protected IEnumerable<object> ResolvedDependencies { get; private set; }

        protected virtual void Create(TSubject subject)
        {
            
        }

        public virtual void Process(TSubject subject, CqrsEngine cqrsEngine)
        {
            
        }


    }
}