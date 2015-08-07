﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using JetBrains.Annotations;
using Microsoft.Data.Entity.ChangeTracking.Internal;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Metadata.Internal;
using Microsoft.Data.Entity.Query.Methods;
using Microsoft.Data.Entity.Storage;
using Microsoft.Framework.Logging;

namespace Microsoft.Data.Entity.Query
{
    public class SqlServerQueryCompilationContextFactory : IQueryCompilationContextFactory
    {
        private readonly IModel _model;
        private readonly ILogger _logger;
        private readonly IEntityMaterializerSource _entityMaterializerSource;
        private readonly IEntityKeyFactorySource _entityKeyFactorySource;
        private readonly IMethodCallTranslator _compositeMethodCallTranslator;
        private readonly IMemberTranslator _compositeMemberTranslator;
        private readonly IClrAccessorSource<IClrPropertyGetter> _clrPropertyGetterSource;
        private readonly IRelationalValueBufferFactoryFactory _valueBufferFactoryFactory;
        private readonly IRelationalTypeMapper _typeMapper;
        private readonly IRelationalMetadataExtensionProvider _relationalExtensions;

        public SqlServerQueryCompilationContextFactory(
            [NotNull] IModel model,
            [NotNull] ILoggerFactory loggerFactory,
            [NotNull] IMethodCallTranslator compositeMethodCallTranslator,
            [NotNull] IMemberTranslator compositeMemberTranslator,
            [NotNull] IEntityMaterializerSource entityMaterializerSource,
            [NotNull] IClrAccessorSource<IClrPropertyGetter> clrPropertyGetterSource,
            [NotNull] IEntityKeyFactorySource entityKeyFactorySource,
            [NotNull] IRelationalValueBufferFactoryFactory valueBufferFactoryFactory,
            [NotNull] IRelationalTypeMapper typeMapper,
            [NotNull] IRelationalMetadataExtensionProvider relationalExtensions)
        {
            _model = model;
            _logger = loggerFactory.CreateLogger<Database>();
            _compositeMethodCallTranslator = compositeMethodCallTranslator;
            _compositeMemberTranslator = compositeMemberTranslator;
            _entityMaterializerSource = entityMaterializerSource;
            _clrPropertyGetterSource = clrPropertyGetterSource;
            _entityKeyFactorySource = entityKeyFactorySource;
            _valueBufferFactoryFactory = valueBufferFactoryFactory;
            _typeMapper = typeMapper;
            _relationalExtensions = relationalExtensions;
        }

        public virtual QueryCompilationContext CreateContext()
            => new SqlServerQueryCompilationContext(
                _model,
                _logger,
                new LinqOperatorProvider(),
                new RelationalResultOperatorHandler(),
                _entityMaterializerSource,
                _entityKeyFactorySource,
                _clrPropertyGetterSource,
                new QueryMethodProvider(),
                _compositeMethodCallTranslator,
                _compositeMemberTranslator,
                _valueBufferFactoryFactory,
                _typeMapper,
                _relationalExtensions);

        public virtual QueryCompilationContext CreateAsyncContext()
            => new SqlServerQueryCompilationContext(
                _model,
                _logger,
                new AsyncLinqOperatorProvider(),
                new RelationalResultOperatorHandler(),
                _entityMaterializerSource,
                _entityKeyFactorySource,
                _clrPropertyGetterSource,
                new AsyncQueryMethodProvider(),
                _compositeMethodCallTranslator,
                _compositeMemberTranslator,
                _valueBufferFactoryFactory,
                _typeMapper,
                _relationalExtensions);
    }
}
