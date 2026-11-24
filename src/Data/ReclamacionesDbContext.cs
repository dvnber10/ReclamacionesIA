using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Business.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Data
{
    public class ReclamacionesDbContext : DbContext
    {
        public ReclamacionesDbContext(DbContextOptions<ReclamacionesDbContext> options)
        : base(options) { }

        public DbSet<Claim> Reclamaciones => Set<Claim>();
        public DbSet<ClaimDocument> Documentos => Set<ClaimDocument>();
        public DbSet<ClaimEvent> Eventos => Set<ClaimEvent>();
        public DbSet<Decision> Decisiones => Set<Decision>();
        public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
        public DbSet<Analyst> Analistas => Set<Analyst>();
        public DbSet<InferenciaIA> Inferencias => Set<InferenciaIA>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                 .Where(e => typeof(EntityBase).IsAssignableFrom(e.ClrType)))
            {
                entityType.FindProperty(nameof(EntityBase.Id))!.ValueGenerated =
                    ValueGenerated.Never;
            }

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReclamacionesDbContext).Assembly);
            base.OnModelCreating(modelBuilder);
        }
    }
}