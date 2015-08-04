// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Query.Compiler;
using Microsoft.Data.Entity.Query.Preprocessor;
using Microsoft.Data.Entity.Utilities;
using System.Linq;

namespace Microsoft.Data.Entity.Query
{
    public class QueryExecutor : IQueryExecutor
    {
        private readonly IQueryContextFactory _contextFactory;
        private readonly IQueryPreprocessor _preprocessor;
        private readonly IQueryCompiler _compiler;

        public QueryExecutor(
            [NotNull] IQueryContextFactory contextFactory,
            [NotNull] IQueryPreprocessor preprocessor,
            [NotNull] IQueryCompiler compiler)
        {
            Check.NotNull(contextFactory, nameof(contextFactory));
            Check.NotNull(preprocessor, nameof(preprocessor));
            Check.NotNull(compiler, nameof(compiler));

            _contextFactory = contextFactory;
            _preprocessor = preprocessor;
            _compiler = compiler;
        }

        public virtual TResult Execute<TResult>([NotNull] Expression query)
        {
            Check.NotNull(query, nameof(query));

            var queryContext = _contextFactory.Create();

            query = _preprocessor.Preprocess(query, queryContext);

            var compiledQuery = _compiler.CompileQuery<TResult>(query);

            return ((Func<QueryContext, TResult>)compiledQuery.Executor)(queryContext);
        }

        public virtual IAsyncEnumerable<TResult> ExecuteAsync<TResult>([NotNull] Expression query)
        {
            Check.NotNull(query, nameof(query));

            var queryContext = _contextFactory.Create();

            query = _preprocessor.Preprocess(query, queryContext);

            var compiledQuery = _compiler.CompileAsyncQuery<TResult>(query);

            return ((Func<QueryContext, IAsyncEnumerable<TResult>>)compiledQuery.Executor)(queryContext);
        }

        public virtual Task<TResult> ExecuteAsync<TResult>([NotNull] Expression query, CancellationToken cancellationToken)
        {
            Check.NotNull(query, nameof(query));

            var queryContext = _contextFactory.Create();
            queryContext.CancellationToken = cancellationToken;

            query = _preprocessor.Preprocess(query, queryContext);

            var compiledQuery = _compiler.CompileAsyncQuery<TResult>(query);

            return ((Func<QueryContext, IAsyncEnumerable<TResult>>)compiledQuery.Executor)(queryContext)
                .First(cancellationToken);
        }
    }
}
