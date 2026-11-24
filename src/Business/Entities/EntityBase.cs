using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Business.Entities
{
    public abstract class EntityBase
    {
        public Guid Id { get; private set; }

        protected EntityBase() => Id = Guid.NewGuid();

        protected EntityBase(Guid id) => Id = id == Guid.Empty ? Guid.NewGuid() : id;
    }
}