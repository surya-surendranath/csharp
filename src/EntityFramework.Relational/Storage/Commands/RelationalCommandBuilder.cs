// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Internal;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Utilities;

namespace Microsoft.Data.Entity.Storage.Commands
{
    public class RelationalCommandBuilder
    {
        private readonly IndentedStringBuilder _stringBuilder = new IndentedStringBuilder();
        private readonly IRelationalTypeMapper _typeMapper;
        private readonly RelationalParameterList _parameterList;

        public RelationalCommandBuilder([NotNull] IRelationalTypeMapper typeMapper)
        {
            Check.NotNull(typeMapper, nameof(typeMapper));

            _typeMapper = typeMapper;

            _parameterList = new RelationalParameterList(
                typeMapper,
                new ParameterNameGenerator());
        }

        public virtual RelationalCommandBuilder AppendLine()
            => AppendLine(string.Empty);

        public virtual RelationalCommandBuilder Append([NotNull]object o)
        {
            Check.NotNull(o, nameof(o));

            _stringBuilder.Append(o);

            return this;
        }

        public virtual RelationalCommandBuilder AppendLine([NotNull]object o)
        {
            Check.NotNull(o, nameof(o));

            _stringBuilder.AppendLine(o);

            return this;
        }

        public virtual RelationalCommandBuilder AppendNamedParameter(
            [NotNull] string name,
            [CanBeNull] object value,
            [CanBeNull] IProperty property = null)
        {
            Check.NotEmpty(name, nameof(name));

            var parameter = _parameterList.GetOrAdd(name, value, property);

            _stringBuilder.Append(parameter.Name);

            return this;
        }

        public virtual RelationalCommandBuilder AppendFormat([NotNull] string sqlFragment, [NotNull] params object[] parameters)
        {
            Check.NotNull(sqlFragment, nameof(sqlFragment));
            Check.NotNull(parameters, nameof(parameters));

            _stringBuilder.Append(FormatParameterString(sqlFragment, parameters));

            return this;
        }

        public virtual RelationalCommandBuilder AppendFormatLine([NotNull] string sqlFragment, [NotNull] params object[] parameters)
        {
            Check.NotNull(sqlFragment, nameof(sqlFragment));
            Check.NotNull(parameters, nameof(parameters));

            _stringBuilder.AppendLine(FormatParameterString(sqlFragment, parameters));

            return this;
        }

        public virtual RelationalCommandBuilder AppendFormatLines([NotNull] string sqlFragment, [NotNull] params object[] parameters)
        {
            Check.NotNull(sqlFragment, nameof(sqlFragment));
            Check.NotNull(parameters, nameof(parameters));

            _stringBuilder.AppendLines(FormatParameterString(sqlFragment, parameters));

            return this;
        }

        public virtual RelationalCommand RelationalCommand
            => new RelationalCommand(
                _stringBuilder.ToString(),
                _parameterList.RelationalParameters.ToArray());

        public virtual RelationalParameterList RelationalParameterList
            => _parameterList;

        public virtual IDisposable Indent()
            => _stringBuilder.Indent();

        private string FormatParameterString(string sqlFragment, params object[] parameters)
        {
            var relationalParameters = _parameterList.Add(parameters);

            return string.Format(sqlFragment, relationalParameters.Select(p => p.Name).ToArray());
        }
    }
}
