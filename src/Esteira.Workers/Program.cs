using Esteira.Application.Messaging;
using Esteira.Infrastructure;
using Esteira.Infrastructure.Media;
using Esteira.Infrastructure.Messaging;
using Esteira.Workers.Consumers;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSerilog((sp, cfg) => cfg.ReadFrom.Configuration(builder.Configuration).WriteTo.Console());

builder.Services.AddEsteiraPersistence(builder.Configuration);
builder.Services.AddEsteiraMedia(builder.Configuration);
builder.Services.AddEsteiraMessaging(builder.Configuration,
    registerConsumers: x =>
    {
        x.AddConsumer<IngestConsumer>();
        x.AddConsumer<TranscribeConsumer>();
        x.AddConsumer<PlanConsumer>();
        x.AddConsumer<RenderConsumer>();
    },
    configureEndpoints: (cfg, context) =>
    {
        cfg.ConfigureStageEndpoint<IngestConsumer>(context, Queues.Ingest);
        cfg.ConfigureStageEndpoint<TranscribeConsumer>(context, Queues.Transcribe);
        cfg.ConfigureStageEndpoint<PlanConsumer>(context, Queues.Plan);
        cfg.ConfigureStageEndpoint<RenderConsumer>(context, Queues.Render);
    });

var host = builder.Build();
host.Run();
