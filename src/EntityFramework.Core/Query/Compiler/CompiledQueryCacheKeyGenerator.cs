// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq.Expressions;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Query.ExpressionVisitors;
using Microsoft.Data.Entity.Utilities;

namespace Microsoft.Data.Entity.Query.Compiler
{
    public class CompiledQueryCacheKeyGenerator : ICompiledQueryCacheKeyGenerator
    {
        private readonly IModel _model;

        public CompiledQueryCacheKeyGenerator([NotNull] IModel model)
        {
            Check.NotNull(model, nameof(model));

            _model = model;
        }

        public virtual string GenerateCacheKey([NotNull] Expression query, bool isAsync)
            => _model.GetHashCode().ToString()
                  + isAsync
                  + new ExpressionStringBuilder()
                      .Build(Check.NotNull(query, nameof(query)));
    }
}
