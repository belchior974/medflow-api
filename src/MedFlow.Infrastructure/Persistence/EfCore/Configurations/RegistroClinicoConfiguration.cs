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
internal sealed class RegistroClinicoConfiguration(bool postgres) : IEntityTypeConfiguration<RegistroClinico>
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

        var dataRegistro = builder.Property(r => r.DataRegistro)
            .HasColumnName("data_registro").IsRequired();

        if (postgres)
        {
            // A tabela e criada por SchemaScripts como TIMESTAMP, ou seja, "timestamp
            // without time zone". Sem declarar isso o Npgsql mapeia DateTime para
            // timestamptz por padrao e recusa gravar valores com Kind=Unspecified
            // ("Cannot write DateTime with Kind=Unspecified..."), derrubando o
            // prontuario com HTTP 500. O DDL escrito a mao e a fonte de verdade aqui:
            // nao ha migrations do EF Core neste projeto.
            dataRegistro.HasColumnType("timestamp without time zone");
        }

        builder.HasIndex(r => new { r.PacienteId, r.DataRegistro })
            .HasDatabaseName("idx_registro_paciente");
    }
}
