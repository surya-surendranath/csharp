// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using JetBrains.Annotations;
using Microsoft.Data.Entity.Storage;
using Microsoft.Data.Entity.Storage.Commands;
using Microsoft.Data.Entity.Utilities;

namespace Microsoft.Data.Entity.Infrastructure
{
    public class RelationalSqlExecutor
    {
        private readonly ISqlStatementExecutor _statementExecutor;
        private readonly IRelationalConnection _connection;
        private readonly IRelationalTypeMapper _typeMapper;

        public RelationalSqlExecutor(
            [NotNull] ISqlStatementExecutor statementExecutor,
            [NotNull] IRelationalConnection connection,
            [NotNull] IRelationalTypeMapper typeMapper)
        {
            _statementExecutor = statementExecutor;
            _connection = connection;
            _typeMapper = typeMapper;
        }

        public virtual void ExecuteSqlCommand([NotNull] string sql, [NotNull] params object[] parameters)
        {
            Check.NotNull(sql, nameof(sql));
            Check.NotNull(parameters, nameof(parameters));

            var builder = new RelationalCommandBuilder(_typeMapper);

            builder.AppendFormat(sql, parameters);

            _statementExecutor.ExecuteNonQuery(_connection, builder.RelationalCommand);
        }
    }
}
