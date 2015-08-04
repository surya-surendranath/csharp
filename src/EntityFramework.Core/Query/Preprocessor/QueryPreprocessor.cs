// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq.Expressions;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Query.ExpressionVisitors;
using Microsoft.Data.Entity.Query.Preprocessor.Internal;
using Microsoft.Data.Entity.Utilities;
using Remotion.Linq.Parsing.ExpressionVisitors.TreeEvaluation;

namespace Microsoft.Data.Entity.Query.Preprocessor
{
    public class QueryPreprocessor : IQueryPreprocessor
    {
        public QueryPreprocessor()
        {
        }

        public virtual Expression Preprocess([NotNull] Expression query, [NotNull] QueryContext queryContext)
        {
            Check.NotNull(query, nameof(query));
            Check.NotNull(queryContext, nameof(queryContext));

            query = new QueryAnnotatingExpressionVisitor().Visit(query);

            query = new FunctionEvaluationDisablingVisitor().Visit(query);

            var partialEvaluationInfo = EvaluatableTreeFindingExpressionVisitor.Analyze(query, new NullEvaluatableExpressionFilter());

            query = new ParameterExtractingExpressionVisitor(partialEvaluationInfo, queryContext).Visit(query);

            return query;
        }
    }
}
