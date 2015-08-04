// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Data.Entity.Query.ExpressionVisitors;
using Remotion.Linq.Parsing.ExpressionVisitors.Transformation;
using Remotion.Linq.Parsing.Structure;
using Remotion.Linq.Parsing.Structure.ExpressionTreeProcessors;

namespace Microsoft.Data.Entity.Query.Compiler.Internal
{
    public class QueryParserFactory
    {
        private static readonly Lazy<ReadonlyNodeTypeProvider> _cachedNodeTypeProvider
            = new Lazy<ReadonlyNodeTypeProvider>(ReadonlyNodeTypeProvider.CreateNodeTypeProvider);

        public static QueryParser Create()
            => new QueryParser(
                new ExpressionTreeParser(
                    _cachedNodeTypeProvider.Value,
                        new CompoundExpressionTreeProcessor(new IExpressionTreeProcessor[]
                        {
                            new PartialEvaluatingExpressionTreeProcessor(new NullEvaluatableExpressionFilter()),
                            new FunctionEvaluationEnablingProcessor(),
                            new TransformingExpressionTreeProcessor(ExpressionTransformerRegistry.CreateDefault())
                        })));
    }
}
