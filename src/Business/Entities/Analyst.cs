using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Business.Entities
{
    public class Analyst : EntityBase
    {
        private Analyst() { } // EF Core

        public Analyst(string email, string nombre, string passwordHash, string rol = "Analista")
        {
            Email = email;
            Nombre = nombre;
            PasswordHash = passwordHash;
            Rol = rol;
        }

        public string Email { get; private set; } = default!;
        public string Nombre { get; private set; } = default!;
        public string PasswordHash { get; private set; } = default!;
        public string Rol { get; private set; } = default!;
    }
}