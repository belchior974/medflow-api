using MedFlow.Domain.Model;
using Microsoft.EntityFrameworkCore;

namespace MedFlow.Infrastructure.Persistence.EfCore;

/// <summary>
/// Contexto do EF Core - responsavel APENAS pelo prontuario.
/// <para>
/// O agendamento fica no Dapper; o prontuario fica aqui porque precisa de consultas
/// dinamicas combinaveis, onde a composicao de <c>IQueryable</c> e claramente superior
/// a montar SQL na mao.
/// </para>
/// </summary>
public sealed class MedFlowDbContext(DbContextOptions<MedFlowDbContext> options) : DbContext(options)
{
    public DbSet<RegistroClinico> RegistrosClinicos => Set<RegistroClinico>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new RegistroClinicoConfiguration(Database.IsNpgsql()));
        base.OnModelCreating(modelBuilder);
    }
}
