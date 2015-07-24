// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Storage;
using Microsoft.Data.Entity.Storage.Commands;
using Microsoft.Data.Entity.Utilities;

namespace Microsoft.Data.Entity.Infrastructure
{
    public class SqlBatchBuilder
    {
        private readonly IRelationalTypeMapper _typeMapper;
        private readonly List<RelationalCommand> _commands = new List<RelationalCommand>();
        private RelationalCommandBuilder _commandBuilder;

        public virtual IReadOnlyList<RelationalCommand> RelationalCommands => _commands;


        public SqlBatchBuilder([NotNull] IRelationalTypeMapper typeMapper)
        {
            Check.NotNull(typeMapper, nameof(typeMapper));

            _typeMapper = typeMapper;
            _commandBuilder = new RelationalCommandBuilder(_typeMapper);
        }

        public virtual SqlBatchBuilder EndBatch()
        {
            var command = _commandBuilder.RelationalCommand;

            if(!string.IsNullOrEmpty(command.CommandText))
            {
                _commands.Add(command);
            }

            _commandBuilder = new RelationalCommandBuilder(_typeMapper);

            return this;
        }

        public virtual SqlBatchBuilder Append([NotNull] object o)
        {
            _commandBuilder.Append(o);

            return this;
        }

        public virtual SqlBatchBuilder AppendLine()
            => AppendLine(string.Empty);

        public virtual SqlBatchBuilder AppendLine([NotNull] object o, bool suppressTransaction = false)
        {
            _commandBuilder.AppendLine(o);

            return this;
        }

        public virtual IDisposable Indent() => _commandBuilder.Indent();
    }
}
