using EventBus.Messages.Common;
using HealthChecks.UI.Client;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Ordering.API.EventBusConsumer;
using Ordering.API.Extensions;
using Ordering.Application.Extensions;
using Ordering.Infrastructure.Data;
using Ordering.Infrastructure.Extensions;
using Polly;

var builder = WebApplication.CreateBuilder(args);

IConfiguration configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json")
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Staging"}.json", true)
        .Build();


// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();


//builder.Services.AddHealthChecks().Services.AddDbContext<OrderContext>();
builder.Services.AddHealthChecks()
    .Services.AddDbContext<OrderContext>(options =>
        options.UseSqlServer(configuration.GetConnectionString("OrderingConnectionString")));




//Add services Extenstion
builder.Services.AddApplicationServices();
builder.Services.AddInfraServices(configuration);
//Add AutoMapper
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());


//Add Versioning
builder.Services.AddApiVersioning();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Ordering.API", Version = "v1" });
});
//Add BasketOrderingConsumer
builder.Services.AddScoped<BasketOrderingConsumer>();
builder.Services.AddMassTransit(config =>
{
    //Mark this as consumer
    config.AddConsumer<BasketOrderingConsumer>();
    //config.AddConsumer<BasketOrderingConsumerV2>();
    config.UsingRabbitMq((ctx, cfg) =>
    {
      

        cfg.Host(configuration.GetValue<string>("EventBusSettings:HostAddress"));
        // provide the queue name with consumer settings
        cfg.ReceiveEndpoint(EventBusConstants.BasketCheckoutQueue, c =>
        {
            c.ConfigureConsumer<BasketOrderingConsumer>(ctx);
        });
        ////V2 endpoint will pick items from here 
        //cfg.ReceiveEndpoint(EventBusConstants.BasketCheckoutQueueV2, c =>
        //{
        //    c.ConfigureConsumer<BasketOrderingConsumerV2>(ctx);
        //});
    });
});

builder.Services.AddMassTransitHostedService();

var app = builder.Build();

app.MigrateDatabase<OrderContext>((context, services) =>
{
    var logger = services.GetService<ILogger<OrderContextSeed>>();
    OrderContextSeed.SeedAsync(context, logger).Wait();
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Ordering.API v1"));
}

app.UseHttpsRedirection();

app.UseAuthorization();


//Map Health Check
app.MapHealthChecks("/health", new HealthCheckOptions()
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
app.MapControllers();
app.Run();
