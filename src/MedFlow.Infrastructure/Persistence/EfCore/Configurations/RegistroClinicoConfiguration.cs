using MedFlow.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedFlow.Infrastructure.Persistence.EfCore;

/// <summary>
/// Todo o mapeamento vive aqui, nao na entidade.
/// <para>
/// E o que permite que <see cref="RegistroClinico"/>, no projeto MedFlow.Domain, nao
/// tenha um unico atributo do EF Core - o dominio nem sequer referencia o pacote.
/// </para>
/// </summary>
internal sealed class RegistroClinicoConfiguration : IEntityTypeConfiguration<RegistroClinico>
{
    public void Configure(EntityTypeBuilder<RegistroClinico> builder)
    {
        builder.ToTable("registro_clinico");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedOnAdd();

        builder.Property(r => r.ConsultaId).HasColumnName("consulta_id").IsRequired();
        builder.Property(r => r.PacienteId).HasColumnName("paciente_id").IsRequired();
        builder.Property(r => r.ProfissionalId).HasColumnName("profissional_id").IsRequired();

        builder.Property(r => r.Diagnostico)
            .HasColumnName("diagnostico").HasMaxLength(500).IsRequired();

        builder.Property(r => r.Prescricao)
            .HasColumnName("prescricao").HasMaxLength(1000);

        builder.Property(r => r.DataRegistro).HasColumnName("data_registro").IsRequired();

        builder.HasIndex(r => new { r.PacienteId, r.DataRegistro })
            .HasDatabaseName("idx_registro_paciente");
    }
}
