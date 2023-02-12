using System;
using System.Linq;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using Shop.Core.Events;
using Shop.Core.Interfaces;
using Shop.Domain.Interfaces.ReadOnly;
using Shop.Domain.Interfaces.WriteOnly;
using Shop.Infrastructure.Behaviors;
using Shop.Infrastructure.Data;
using Shop.Infrastructure.Data.Context;
using Shop.Infrastructure.Data.Repositories;
using Shop.Infrastructure.Data.Repositories.ReadOnly;
using Shop.Infrastructure.Data.Repositories.WriteOnly;

namespace Shop.Infrastructure;

public static class ServicesCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // MediatR Pipelines
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

        // Repositories
        services.AddScoped<IEventStoreRepository, EventStoreRepository>();
        services.AddScoped<ICustomerWriteOnlyRepository, CustomerWriteOnlyRepository>();
        services.AddScoped<ICustomerReadOnlyRepository, CustomerReadOnlyRepository>();

        // Database Contexts
        services.AddScoped<WriteDbContext>();
        services.AddScoped<ReadDbContext>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ISyncDataBase, NoSqlSyncDataBase>();

        ConfigureMongoDB();

        return services;
    }

    private static void ConfigureMongoDB()
    {
        // Passo 1º: Configurar o tipo do serializador de Guid.
        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.CSharpLegacy));

        // Passo 2º: Configurar as convenções, assim será aplicado para todos os mapeamentos.
        // REF: https://mongodb.github.io/mongo-csharp-driver/2.0/reference/bson/mapping/conventions/
        ConventionRegistry.Register("Conventions", new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new EnumRepresentationConvention(BsonType.String),
            new IgnoreExtraElementsConvention(true),
            new IgnoreIfNullConvention(true)
        }, _ => true);

        // Passo 3º: Registrar as configurações dos mapeamento das classes.
        // É recomendável registrar todos os mapeamentos antes de inicializar a conexão com o MongoDb
        // REF: https://mongodb.github.io/mongo-csharp-driver/2.0/reference/bson/mapping/
        ApplyMongoDbMappingsFromAssembly();
    }

    private static void ApplyMongoDbMappingsFromAssembly()
    {
        var implementations = AppDomain
            .CurrentDomain
            .GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(IReadDbMapping).IsAssignableFrom(type)
                && type.IsClass
                && !type.IsAbstract
                && !type.IsInterface)
            .ToList();

        foreach (var impl in implementations)
        {
            var mapping = (IReadDbMapping)Activator.CreateInstance(impl);
            mapping.Configure();
        }
    }
}