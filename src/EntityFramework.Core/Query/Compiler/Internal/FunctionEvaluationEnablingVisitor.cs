// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq.Expressions;
using Microsoft.Data.Entity.Query.Expressions;
using Microsoft.Data.Entity.Query.ExpressionVisitors;
using Remotion.Linq.Clauses.Expressions;

namespace Microsoft.Data.Entity.Query.Compiler.Internal
{
    public class FunctionEvaluationEnablingVisitor : ExpressionVisitorBase
    {
        protected override Expression VisitExtension(Expression expression)
        {
            var methodCallWrapper = expression as MethodCallEvaluationPreventingExpression;
            if (methodCallWrapper != null)
            {
                return Visit(methodCallWrapper.MethodCall);
            }

            var propertyWrapper = expression as PropertyEvaluationPreventingExpression;
            if (propertyWrapper != null)
            {
                return Visit(propertyWrapper.MemberExpression);
            }

            return base.VisitExtension(expression);
        }

        protected override Expression VisitSubQuery(SubQueryExpression expression)
        {
            var clonedModel = expression.QueryModel.Clone();
            clonedModel.TransformExpressions(Visit);

            return new SubQueryExpression(clonedModel);
        }
    }
}
