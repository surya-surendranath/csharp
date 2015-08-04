// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq.Expressions;
using Remotion.Linq.Parsing.Structure;

namespace Microsoft.Data.Entity.Query.Compiler.Internal
{
    public class FunctionEvaluationEnablingProcessor : IExpressionTreeProcessor
    {
        public virtual Expression Process(Expression expressionTree)
        {
            return new FunctionEvaluationEnablingVisitor().Visit(expressionTree);
        }
    }
}
