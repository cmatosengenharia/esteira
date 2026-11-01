using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Esteira.Infrastructure.Messaging;

public static class MessagingExtensions
{
    /// <summary>
    /// MassTransit + RabbitMQ. A API chama sem consumers (só publica). Os Workers passam os consumers e as filas.
    /// Política por fila: 3 retentativas com backoff exponencial; esgotadas, a mensagem vai para a fila de erro (dead-letter "&lt;fila&gt;_error").
    /// </summary>
    public static IServiceCollection AddEsteiraMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? registerConsumers = null,
        Action<IRabbitMqBusFactoryConfigurator, IBusRegistrationContext>? configureEndpoints = null)
    {
        var options = configuration.GetSection(RabbitMqOptions.Section).Get<RabbitMqOptions>() ?? new RabbitMqOptions();

        services.AddMassTransit(x =>
        {
            registerConsumers?.Invoke(x);
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(options.Host, options.Port, options.VirtualHost, h =>
                {
                    h.Username(options.Username);
                    h.Password(options.Password);
                });
                configureEndpoints?.Invoke(cfg, context);
            });
        });
        return services;
    }

    public const int MaxRetries = 3;

    /// <summary>Configuração padrão de uma fila de etapa.</summary>
    public static void ConfigureStageEndpoint<TConsumer>(this IRabbitMqBusFactoryConfigurator cfg, IBusRegistrationContext context, string queueName)
        where TConsumer : class, IConsumer
    {
        cfg.ReceiveEndpoint(queueName, e =>
        {
            e.PrefetchCount = 1;
            e.ConcurrentMessageLimit = 1;
            e.UseMessageRetry(r => r.Exponential(MaxRetries, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5)));
            e.ConfigureConsumer<TConsumer>(context);
        });
    }
}
