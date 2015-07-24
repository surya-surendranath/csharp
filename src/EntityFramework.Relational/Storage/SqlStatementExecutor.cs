// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Internal;
using Microsoft.Data.Entity.Storage.Commands;
using Microsoft.Data.Entity.Utilities;
using Microsoft.Framework.Logging;

namespace Microsoft.Data.Entity.Storage
{
    public class SqlStatementExecutor : ISqlStatementExecutor
    {
        private readonly LazyRef<ILogger> _logger;

        public SqlStatementExecutor([NotNull] ILoggerFactory loggerFactory)
        {
            Check.NotNull(loggerFactory, nameof(loggerFactory));

            _logger = new LazyRef<ILogger>(loggerFactory.CreateLogger<SqlStatementExecutor>);
        }

        protected virtual ILogger Logger => _logger.Value;

        public virtual void ExecuteNonQuery(
            [NotNull] IRelationalConnection connection,
            [NotNull] RelationalCommand relationalCommand)
            => ExecuteNonQuery(connection, new[] { relationalCommand });

        public virtual void ExecuteNonQuery(
            [NotNull] IRelationalConnection connection,
            [NotNull] IEnumerable<RelationalCommand> relationalCommands)
        {
            Check.NotNull(connection, nameof(connection));
            Check.NotNull(relationalCommands, nameof(relationalCommands));

            Execute<object>(
                connection,
                () =>
                {
                    foreach (var relationalCommand in relationalCommands)
                    {
                        var command = relationalCommand.CreateDbCommand(connection);
                        Logger.LogCommand(command);

                        command.ExecuteNonQuery();
                    }
                    return null;
                });
        }

        public virtual Task ExecuteNonQueryAsync(
            [NotNull] IRelationalConnection connection,
            [NotNull] RelationalCommand relationalCommand,
            CancellationToken cancellationToken = default(CancellationToken))
            => ExecuteNonQueryAsync(connection, new[] { relationalCommand }, cancellationToken);

        public virtual Task ExecuteNonQueryAsync(
            [NotNull] IRelationalConnection connection,
            [NotNull] IEnumerable<RelationalCommand> relationalCommands,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Check.NotNull(connection, nameof(connection));
            Check.NotNull(relationalCommands, nameof(relationalCommands));

            return ExecuteAsync(
                connection,
                async () =>
                {
                    foreach (var relationalCommand in relationalCommands)
                    {
                        var command = relationalCommand.CreateDbCommand(connection);
                        Logger.LogCommand(command);

                        await command.ExecuteNonQueryAsync(cancellationToken);
                    }
                    return Task.FromResult<object>(null);
                },
                cancellationToken);
        }

        public virtual object ExecuteScalar(
            IRelationalConnection connection,
            string sql)
        {
            Check.NotNull(connection, nameof(connection));
            Check.NotNull(sql, nameof(sql));

            return Execute(
                connection,
                () =>
                {
                    var command = new RelationalCommand(sql).CreateDbCommand(connection);
                    Logger.LogCommand(command);

                    return command.ExecuteScalar();
                });
        }

        public virtual Task<object> ExecuteScalarAsync(
            IRelationalConnection connection,
            string sql,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Check.NotNull(connection, nameof(connection));
            Check.NotNull(sql, nameof(sql));

            return ExecuteAsync(
                connection,
                () =>
                    {
                        var command = new RelationalCommand(sql).CreateDbCommand(connection);
                        Logger.LogCommand(command);

                        return command.ExecuteScalarAsync(cancellationToken);
                    },
                cancellationToken);
        }

        public virtual DbDataReader ExecuteReader(
            [NotNull] IRelationalConnection connection,
            [NotNull] string sql)
        {
            Check.NotNull(connection, nameof(connection));
            Check.NotNull(sql, nameof(sql));

            return Execute(
                connection,
                () =>
                    {
                        var command = new RelationalCommand(sql).CreateDbCommand(connection);
                        Logger.LogCommand(command);

                        return command.ExecuteReader();
                    });
        }

        public virtual Task<DbDataReader> ExecuteReaderAsync(
            [NotNull] IRelationalConnection connection,
            [NotNull] string sql,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Check.NotNull(connection, nameof(connection));
            Check.NotNull(sql, nameof(sql));

            return ExecuteAsync(
                connection,
                () =>
                    {
                        var command = new RelationalCommand(sql).CreateDbCommand(connection);
                        Logger.LogCommand(command);

                        return command.ExecuteReaderAsync(cancellationToken);
                    },
                cancellationToken);
        }

        protected virtual T Execute<T>(
            IRelationalConnection connection,
            Func<T> action)
        {
            Check.NotNull(connection, nameof(connection));

            // TODO Deal with suppressing transactions etc.
            connection.Open();

            try
            {
                return action();
            }
            finally
            {
                connection.Close();
            }
        }

        protected virtual async Task<T> ExecuteAsync<T>(
            IRelationalConnection connection,
            Func<Task<T>> action,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Check.NotNull(connection, nameof(connection));

            await connection.OpenAsync(cancellationToken);

            try
            {
                return await action();
            }
            finally
            {
                connection.Close();
            }
        }


    }
}
