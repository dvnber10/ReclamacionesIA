using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Business.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Data.Configurations
{
    internal sealed class ClaimConfiguration : IEntityTypeConfiguration<Claim>
    {
        public void Configure(EntityTypeBuilder<Claim> b)
        {
            b.ToTable("reclamaciones");
            b.HasKey(x => x.Id);

            b.Property(x => x.PublicToken).IsRequired().HasMaxLength(100);
            b.HasIndex(x => x.PublicToken).IsUnique();

            // Postgres permite varios NULL en un índice único: no hace falta filtro.
            b.Property(x => x.ExternalEventId).HasMaxLength(200);
            b.HasIndex(x => x.ExternalEventId).IsUnique();

            b.Property(x => x.BeneficiaryName).IsRequired().HasMaxLength(200);
            b.Property(x => x.BeneficiaryDocument).IsRequired().HasMaxLength(50);
            b.Property(x => x.BeneficiaryEmail).HasMaxLength(200);
            b.Property(x => x.BeneficiaryPhone).HasMaxLength(50);
            b.Property(x =>x.TelegramChatId).HasMaxLength(64);
            b.Property(x => x.InsuredName).IsRequired().HasMaxLength(200);
            b.Property(x => x.InsuredDocument).IsRequired().HasMaxLength(50);
            b.Property(x => x.FreeText).IsRequired();

            b.Property(x => x.InferenciaRaw).HasColumnType("text");
            b.Property(x => x.CompletadoEl).HasColumnType("date");
            b.Property(x => x.FechaLimite).HasColumnType("date");

            b.HasMany(x => x.Documentos).WithOne()
                .HasForeignKey(x => x.ClaimId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Eventos).WithOne()
                .HasForeignKey(x => x.ClaimId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Decisiones).WithOne()
                .HasForeignKey(x => x.ClaimId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    internal sealed class ClaimDocumentConfiguration : IEntityTypeConfiguration<ClaimDocument>
    {
        public void Configure(EntityTypeBuilder<ClaimDocument> b)
        {
            b.ToTable("documentos");
            b.HasKey(x => x.Id);

            b.Property(x => x.NombreArchivo).IsRequired().HasMaxLength(300);
            b.Property(x => x.ContentHash).IsRequired().HasMaxLength(64);
            b.Property(x => x.Ruta).IsRequired().HasMaxLength(500);
            b.Property(x => x.ContentType).IsRequired().HasMaxLength(100);
            b.Property(x => x.ExtraidoJson).HasColumnType("text");

            // 🔑 Idempotencia: el mismo archivo dos veces no crea otro documento.
            b.HasIndex(x => new { x.ClaimId, x.ContentHash }).IsUnique();
        }
    }

    internal sealed class ClaimEventConfiguration : IEntityTypeConfiguration<ClaimEvent>
    {
        public void Configure(EntityTypeBuilder<ClaimEvent> b)
        {
            b.ToTable("eventos");
            b.HasKey(x => x.Id);
            b.Property(x => x.Tipo).IsRequired().HasMaxLength(100);
            b.Property(x => x.Actor).IsRequired().HasMaxLength(100);
            b.Property(x => x.PayloadJson).IsRequired().HasColumnType("text");
            b.HasIndex(x => new { x.ClaimId, x.OcurridoEn });
        }
    }

    internal sealed class DecisionConfiguration : IEntityTypeConfiguration<Decision>
    {
        public void Configure(EntityTypeBuilder<Decision> b)
        {
            b.ToTable("decisiones");
            b.HasKey(x => x.Id);
            b.Property(x => x.AnalistaNombre).IsRequired().HasMaxLength(200);
            b.Property(x => x.Notas).HasColumnType("text");
            b.Property(x => x.MontoIndemnizacion).HasColumnType("numeric(18,2)");
            b.HasIndex(x => x.ClaimId);
        }
    }

    internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
    {
        public void Configure(EntityTypeBuilder<OutboxMessage> b)
        {
            b.ToTable("outbox");
            b.HasKey(x => x.Id);
            b.Property(x => x.Destino).IsRequired().HasMaxLength(200);
            b.Property(x => x.PayloadJson).IsRequired().HasColumnType("text");
            b.Property(x => x.UltimoError).HasColumnType("text");

            // El worker busca pendientes con este índice.
            b.HasIndex(x => new { x.Estado, x.CreadoEn });
            b.HasOne<Claim>().WithMany().HasForeignKey(x => x.ClaimId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    internal sealed class AnalystConfiguration : IEntityTypeConfiguration<Analyst>
    {
        public void Configure(EntityTypeBuilder<Analyst> b)
        {
            b.ToTable("analistas");
            b.HasKey(x => x.Id);
            b.Property(x => x.Email).IsRequired().HasMaxLength(200);
            b.HasIndex(x => x.Email).IsUnique();
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(200);
            b.Property(x => x.PasswordHash).IsRequired();
            b.Property(x => x.Rol).IsRequired().HasMaxLength(50);
        }
    }

    internal sealed class InferenciaIAConfiguration : IEntityTypeConfiguration<InferenciaIA>
    {
        public void Configure(EntityTypeBuilder<InferenciaIA> b)
        {
            b.ToTable("inferencias_ia");
            b.HasKey(x => x.Id);
            b.Property(x => x.Modelo).IsRequired().HasMaxLength(100);
            b.Property(x => x.PromptVersion).IsRequired().HasMaxLength(20);
            b.Property(x => x.SolicitudJson).IsRequired().HasColumnType("text");
            b.Property(x => x.RespuestaRaw).HasColumnType("text");
            b.Property(x => x.Error).HasColumnType("text");
            b.HasIndex(x => new { x.ClaimId, x.CreadoEn });
            b.HasOne<Claim>().WithMany().HasForeignKey(x => x.ClaimId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

}