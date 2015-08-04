// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Utilities;
using Remotion.Linq;

namespace Microsoft.Data.Entity.Query.Compiler
{
    public class InMemoryQueryCompiler : QueryCompiler
    {
        public InMemoryQueryCompiler(
            [NotNull] ICompiledQueryCache queryCache,
            [NotNull] ICompiledQueryCacheKeyGenerator cacheKeyGenerator,
            [NotNull] IQueryCompilationContextFactory compilationContextFactory)
            : base(queryCache, cacheKeyGenerator, compilationContextFactory)
        {
        }

        protected override Func<QueryContext, IAsyncEnumerable<TResult>> CreateAsyncQueryExecutor<TResult>(
            [NotNull] QueryModel queryModel)
        {
            Check.NotNull(queryModel, nameof(queryModel));

            var syncQueryExecutor = CreateQueryExecutor<TResult>(queryModel);

            return qc => syncQueryExecutor(qc).ToAsyncEnumerable();
        }
    }
}
