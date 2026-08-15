using MedFlow.Application.Ports;
using MedFlow.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;

namespace MedFlow.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registra os use cases. Note que esta camada nao registra NENHUMA implementacao
    /// de porta - ela apenas as consome. Quem satisfaz as portas e a infraestrutura.
    /// </summary>
    public static IServiceCollection AddMedFlowApplication(this IServiceCollection services)
    {
        services.AddScoped<AgendarConsultaUseCase>();
        services.AddScoped<GerenciarConsultaUseCase>();
        services.AddScoped<ConsultarDisponibilidadeUseCase>();
        // A mesma instancia satisfaz a porta estreita usada por quem so invalida.
        services.AddScoped<IDisponibilidadeCache>(sp =>
            sp.GetRequiredService<ConsultarDisponibilidadeUseCase>());
        services.AddScoped<ProntuarioUseCase>();
        services.AddScoped<CadastrarPacienteUseCase>();
        services.AddScoped<CadastrarProfissionalUseCase>();
        return services;
    }
}
