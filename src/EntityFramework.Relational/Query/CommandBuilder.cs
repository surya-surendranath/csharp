// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Query.Sql;
using Microsoft.Data.Entity.Storage;
using Microsoft.Data.Entity.Utilities;

namespace Microsoft.Data.Entity.Query
{
    public class CommandBuilder
    {
        private readonly IRelationalValueBufferFactoryFactory _valueBufferFactoryFactory;
        private readonly Func<IQueryCommandGenerator> _commandGeneratorFactory;

        private IRelationalValueBufferFactory _valueBufferFactory;

        public CommandBuilder(
            [NotNull] Func<IQueryCommandGenerator> commandGeneratorFactory,
            [NotNull] IRelationalValueBufferFactoryFactory valueBufferFactoryFactory)
        {
            Check.NotNull(commandGeneratorFactory, nameof(commandGeneratorFactory));
            Check.NotNull(valueBufferFactoryFactory, nameof(valueBufferFactoryFactory));

            _commandGeneratorFactory = commandGeneratorFactory;
            _valueBufferFactoryFactory = valueBufferFactoryFactory;
        }

        public virtual IRelationalValueBufferFactory ValueBufferFactory => _valueBufferFactory;

        public virtual Func<IQueryCommandGenerator> CommandGeneratorFactory => _commandGeneratorFactory;

        public virtual DbCommand Build(
            [NotNull] IRelationalConnection connection,
            [NotNull] IDictionary<string, object> parameterValues)
        {
            Check.NotNull(connection, nameof(connection));

            return _commandGeneratorFactory()
                .GenerateCommand(parameterValues)
                .CreateDbCommand(connection);
        }

        public virtual void NotifyReaderCreated([NotNull] DbDataReader dataReader)
        {
            Check.NotNull(dataReader, nameof(dataReader));

            LazyInitializer
                .EnsureInitialized(
                    ref _valueBufferFactory,
                    () => _commandGeneratorFactory()
                        .CreateValueBufferFactory(_valueBufferFactoryFactory, dataReader));
        }
    }
}
