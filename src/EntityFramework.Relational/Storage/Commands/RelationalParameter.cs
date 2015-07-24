// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Common;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Utilities;

namespace Microsoft.Data.Entity.Storage.Commands
{
    public class RelationalParameter
    {
        private readonly IRelationalTypeMapper _typeMapper;
        private readonly IProperty _property;

        public RelationalParameter(
            [NotNull] IRelationalTypeMapper typeMapper,
            [NotNull] string name,
            [CanBeNull] object value,
            [CanBeNull] IProperty property = null)
        {
            Check.NotNull(typeMapper, nameof(typeMapper));
            Check.NotNull(name, nameof(name));

            Name = name;
            Value = value;
            _typeMapper = typeMapper;
            _property = property;
        }

        public virtual string Name { get; }

        public virtual object Value { get; }

        public virtual DbParameter CreateDbParameter([NotNull] DbCommand command)
        {
            Check.NotNull(command, nameof(command));

            return _property == null
                ? _typeMapper.GetDefaultMapping(Value)
                    .CreateParameter(
                        command,
                        Name,
                        Value ?? DBNull.Value,
                        Value.GetType().IsNullableType())
                : _typeMapper.MapPropertyType(_property)
                    .CreateParameter(
                        command,
                        Name,
                        Value ?? DBNull.Value,
                        _property.IsNullable);
        }
    }
}
