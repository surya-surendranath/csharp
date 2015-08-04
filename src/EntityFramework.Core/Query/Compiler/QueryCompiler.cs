// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Query.Compiler.Internal;
using Microsoft.Data.Entity.Utilities;
using Remotion.Linq;
using Remotion.Linq.Clauses.StreamedData;

namespace Microsoft.Data.Entity.Query.Compiler
{
    public class QueryCompiler : IQueryCompiler
    {
        private readonly ICompiledQueryCache _cache;
        private readonly ICompiledQueryCacheKeyGenerator _cacheKeyGenerator;
        private readonly IQueryCompilationContextFactory _compilationContextFactory;

        private static MethodInfo CompileQueryMethod { get; }
            = typeof(QueryCompiler).GetTypeInfo().GetDeclaredMethod("CreateQueryExecutor");

        public QueryCompiler(
            [NotNull] ICompiledQueryCache cache,
            [NotNull] ICompiledQueryCacheKeyGenerator cacheKeyGenerator,
            [NotNull] IQueryCompilationContextFactory compilationContextFactory)
        {
            Check.NotNull(cache, nameof(cache));
            Check.NotNull(cacheKeyGenerator, nameof(cacheKeyGenerator));
            Check.NotNull(compilationContextFactory, nameof(compilationContextFactory));

            _cache = cache;
            _cacheKeyGenerator = cacheKeyGenerator;
            _compilationContextFactory = compilationContextFactory;
        }

        CompiledQuery IQueryCompiler.CompileQuery<TResult>([NotNull] Expression query)
        {
            Check.NotNull(query, nameof(query));

            return _cache.GetOrAdd(_cacheKeyGenerator.GenerateCacheKey(query, isAsync: false), () =>
                {
                    var queryModel = QueryParserFactory.Create().GetParsedQuery(query);

                    var resultItemType
                        = (queryModel.GetOutputDataInfo() as StreamedSequenceInfo)?.ResultItemType ?? typeof(TResult);

                    return new CompiledQuery
                    {
                        ResultItemType = resultItemType,
                        Executor = MapQueryExecutor<TResult>(queryModel, resultItemType)
                    };
                });
        }

        CompiledQuery IQueryCompiler.CompileAsyncQuery<TResult>([NotNull] Expression query)
        {
            Check.NotNull(query, nameof(query));

            return _cache.GetOrAdd(_cacheKeyGenerator.GenerateCacheKey(query, isAsync: true), () =>
                {
                    var queryModel = QueryParserFactory.Create().GetParsedQuery(query);

                    return new CompiledQuery
                    {
                        ResultItemType = typeof(TResult),
                        Executor = CreateAsyncQueryExecutor<TResult>(queryModel)
                    };
                });
        }

        protected virtual Func<QueryContext, IEnumerable<TResult>> CreateQueryExecutor<TResult>(
            [NotNull] QueryModel queryModel)
            => _compilationContextFactory.CreateContext()
                .CreateQueryModelVisitor()
                .CreateQueryExecutor<TResult>(
                    Check.NotNull(queryModel, nameof(queryModel)));

        protected virtual Func<QueryContext, IAsyncEnumerable<TResult>> CreateAsyncQueryExecutor<TResult>(
            [NotNull] QueryModel queryModel)
            => _compilationContextFactory.CreateAsyncContext()
                .CreateQueryModelVisitor()
                .CreateAsyncQueryExecutor<TResult>(
                    Check.NotNull(queryModel, nameof(queryModel)));

        private Func<QueryContext, TResult> MapQueryExecutor<TResult>(QueryModel queryModel, Type resultItemType)
        {
            if (resultItemType == typeof(TResult))
            {
                var func = CreateQueryExecutor<TResult>(queryModel);
                return qc => func(qc).First();
            }
            else
            {
                try
                {
                    return (Func<QueryContext, TResult>)CompileQueryMethod
                        .MakeGenericMethod(resultItemType)
                        .Invoke(this, new object[] { queryModel });
                }
                catch (TargetInvocationException e)
                {
                    ExceptionDispatchInfo.Capture(e.InnerException).Throw();

                    throw;
                }
            }
        }
    }
}
