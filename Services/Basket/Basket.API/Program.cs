using Basket.Application.GrpcService;
using Basket.Application.Handlers;
using Basket.Core.Repositories;
using Basket.Infrastructure.Repositories;
using Discount.Grpc.Protos;
using EventBus.Messages.Common;
using HealthChecks.UI.Client;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

IConfiguration configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json")
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Staging"}.json", true)
        .Build();



// Add services to the container.

//builder.Services.AddControllers();

//Redis Settings
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = configuration.GetValue<string>("CacheSettings:ConnectionString");
});



//Add Health Check
builder.Services.AddHealthChecks().AddRedis(configuration.GetValue<string>("CacheSettings:ConnectionString"), "Redis Health",
                HealthStatus.Degraded);


//Add Versioning
builder.Services.AddApiVersioning();

//Add AutoMapper
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

//MediatorR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateShoppingCartCommandHandler).Assembly));

//Services
builder.Services.AddScoped<IBasketRepository, BasketRepository>();
builder.Services.AddScoped<DiscountGrpcService>();
builder.Services.AddGrpcClient<DiscountProtoService.DiscountProtoServiceClient>
            (o => o.Address = new Uri(configuration.GetValue<string>("GrpcSettings:DiscountUrl")));


// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddMassTransit(config =>
{
    //Mark this as consumer
    //config.AddConsumer<BasketOrderingConsumer>();
    //config.AddConsumer<BasketOrderingConsumerV2>();
    config.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(configuration.GetValue<string>("EventBusSettings:HostAddress"));
        //provide the queue name with consumer settings
        //cfg.ReceiveEndpoint(EventBusConstants.BasketCheckoutQueue, c =>
        //{
        //    c.ConfigureConsumer<BasketOrderingConsumer>(ctx);
        //});
        ////V2 endpoint will pick items from here 
        //cfg.ReceiveEndpoint(EventBusConstants.BasketCheckoutQueueV2, c =>
        //{
        //    c.ConfigureConsumer<BasketOrderingConsumerV2>(ctx);
        //});
    });
});


//Identity Server Changes

var userPolicy = new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .Build();

builder.Services.AddControllers(config =>
{
    config.Filters.Add(new AuthorizeFilter(userPolicy));
});
//
//
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                 .AddJwtBearer(options =>
                 {
                     options.Authority = "https://localhost:9009";
                     options.Audience = "Basket";
                 });


builder.Services.AddMassTransitHostedService();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

//Map Health Check
app.MapHealthChecks("/health", new HealthCheckOptions()
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.Run();
