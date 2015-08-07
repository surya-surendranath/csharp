// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using JetBrains.Annotations;
using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Data.Entity.Query;
using Microsoft.Data.Entity.SqlServer;
using Microsoft.Data.Entity.SqlServer.Metadata;
using Microsoft.Data.Entity.SqlServer.Migrations;
using Microsoft.Data.Entity.SqlServer.Update;
using Microsoft.Data.Entity.SqlServer.ValueGeneration;
using Microsoft.Data.Entity.Storage;
using Microsoft.Data.Entity.Update;
using Microsoft.Data.Entity.Utilities;

// ReSharper disable once CheckNamespace

namespace Microsoft.Framework.DependencyInjection
{
    public static class SqlServerEntityFrameworkServicesBuilderExtensions
    {
        public static EntityFrameworkServicesBuilder AddSqlServer([NotNull] this EntityFrameworkServicesBuilder builder)
        {
            Check.NotNull(builder, nameof(builder));

            var service = builder.AddRelational().GetService();

            service.TryAddEnumerable(ServiceDescriptor
                .Singleton<IDatabaseProvider, DatabaseProvider<SqlServerDatabaseProviderServices, SqlServerOptionsExtension>>());

            service.TryAdd(new ServiceCollection()
                    .AddSingleton<SqlServerConventionSetBuilder>()
                    .AddSingleton<ISqlServerValueGeneratorCache, SqlServerValueGeneratorCache>()
                    .AddSingleton<ISqlServerUpdateSqlGenerator, SqlServerUpdateSqlGenerator>()
                    .AddSingleton<SqlServerTypeMapper>()
                    .AddSingleton<SqlServerModelSource>()
                    .AddSingleton<SqlServerMetadataExtensionProvider>()
                    .AddSingleton<SqlServerMigrationAnnotationProvider>()
                    .AddScoped<ISqlServerSequenceValueGeneratorFactory, SqlServerSequenceValueGeneratorFactory>()
                    .AddScoped<SqlServerModificationCommandBatchFactory>()
                    .AddScoped<SqlServerValueGeneratorSelector>()
                    .AddScoped<SqlServerDatabaseProviderServices>()
                    .AddScoped<ISqlServerConnection, SqlServerConnection>()
                    .AddScoped<SqlServerMigrationSqlGenerator>()
                    .AddScoped<SqlServerDatabaseCreator>()
                    .AddScoped<SqlServerHistoryRepository>()
                    .AddScoped<SqlServerCompositeMethodCallTranslator>()
                    .AddScoped<SqlServerCompositeMemberTranslator>()
                    .AddQuery());

            return builder;
        }

        private static IServiceCollection AddQuery(this IServiceCollection serviceCollection)
        {
            return serviceCollection
                .AddScoped<SqlServerQueryCompilationContextFactory>();
        }
    }
}
