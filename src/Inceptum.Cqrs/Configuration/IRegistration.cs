using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Inceptum.Cqrs.InfrastructureCommands;

namespace Inceptum.Cqrs.Configuration
{
    public interface IRegistration
    {
        void Create(CqrsEngine cqrsEngine);
        void Process(CqrsEngine cqrsEngine);
        IEnumerable<Type> Dependencies { get; }
    }
 
    

}