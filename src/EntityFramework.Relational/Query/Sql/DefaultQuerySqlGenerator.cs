// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Query.Expressions;
using Microsoft.Data.Entity.Query.ExpressionVisitors;
using Microsoft.Data.Entity.Storage;
using Microsoft.Data.Entity.Storage.Commands;
using Microsoft.Data.Entity.Utilities;
using Remotion.Linq.Clauses;
using Remotion.Linq.Parsing;

namespace Microsoft.Data.Entity.Query.Sql
{
    public class DefaultQuerySqlGenerator : ThrowingExpressionVisitor, ISqlExpressionVisitor, IQueryCommandGenerator
    {
        private readonly SelectExpression _selectExpression;
        private readonly IRelationalTypeMapper _typeMapper;

        private RelationalCommandBuilder _commandBuilder;
        private IDictionary<string, object> _parameterValues;

        public DefaultQuerySqlGenerator(
            [NotNull] SelectExpression selectExpression,
            [CanBeNull] IRelationalTypeMapper typeMapper)
        {
            Check.NotNull(selectExpression, nameof(selectExpression));

            _selectExpression = selectExpression;
            _typeMapper = typeMapper;
        }

        public virtual SelectExpression SelectExpression => _selectExpression;

        public virtual RelationalCommand GenerateCommand([NotNull] IDictionary<string, object> parameterValues)
        {
            Check.NotNull(parameterValues, nameof(parameterValues));

            _commandBuilder = new RelationalCommandBuilder(_typeMapper);
            _parameterValues = parameterValues;

            Visit(_selectExpression);

            return _commandBuilder.RelationalCommand;
        }

        public virtual IRelationalValueBufferFactory CreateValueBufferFactory(
            IRelationalValueBufferFactoryFactory relationalValueBufferFactoryFactory, DbDataReader _)
        {
            Check.NotNull(relationalValueBufferFactoryFactory, nameof(relationalValueBufferFactoryFactory));

            return relationalValueBufferFactoryFactory
                .Create(_selectExpression.GetProjectionTypes().ToArray(), indexMap: null);
        }

        protected virtual RelationalCommandBuilder CommandBuilder => _commandBuilder;

        protected virtual string ConcatOperator => "+";

        protected virtual string TrueLiteral => "1";
        protected virtual string FalseLiteral => "0";
        protected virtual string TypedTrueLiteral => "CAST(1 AS BIT)";
        protected virtual string TypedFalseLiteral => "CAST(0 AS BIT)";

        public virtual Expression VisitSelect(SelectExpression selectExpression)
        {
            Check.NotNull(selectExpression, nameof(selectExpression));

            IDisposable subQueryIndent = null;

            if (selectExpression.Alias != null)
            {
                CommandBuilder.AppendLine("(");

                subQueryIndent = CommandBuilder.Indent();
            }

            CommandBuilder.Append("SELECT ");

            if (selectExpression.IsDistinct)
            {
                CommandBuilder.Append("DISTINCT ");
            }

            GenerateTop(selectExpression);

            if (selectExpression.Projection.Any())
            {
                VisitJoin(selectExpression.Projection);
            }
            else if (selectExpression.IsProjectStar)
            {
                CommandBuilder.Append(DelimitIdentifier(selectExpression.Tables.Single().Alias))
                    .Append(".*");
            }
            else
            {
                CommandBuilder.Append("1");
            }

            if (selectExpression.Tables.Any())
            {
                CommandBuilder.AppendLine()
                    .Append("FROM ");

                VisitJoin(selectExpression.Tables, sql => sql.AppendLine());
            }

            if (selectExpression.Predicate != null)
            {
                CommandBuilder.AppendLine()
                    .Append("WHERE ");

                var constantExpression = selectExpression.Predicate as ConstantExpression;

                if (constantExpression != null)
                {
                    CommandBuilder.Append((bool)constantExpression.Value ? "1 = 1" : "1 = 0");
                }
                else
                {
                    var predicate
                        = new NullComparisonTransformingVisitor(_parameterValues)
                            .Visit(selectExpression.Predicate);

                    // we have to optimize out comparisons to null-valued parameters before we can expand null semantics 
                    if (_parameterValues.Count > 0)
                    {
                        var optimizedNullExpansionVisitor = new NullSemanticsOptimizedExpandingVisitor();
                        var nullSemanticsExpandedOptimized = optimizedNullExpansionVisitor.Visit(predicate);
                        if (optimizedNullExpansionVisitor.OptimizedExpansionPossible)
                        {
                            predicate = nullSemanticsExpandedOptimized;
                        }
                        else
                        {
                            predicate = new NullSemanticsExpandingVisitor()
                                .Visit(predicate);
                        }
                    }

                    predicate = new ReducingExpressionVisitor().Visit(predicate);

                    Visit(predicate);

                    if (selectExpression.Predicate is ParameterExpression
                        || selectExpression.Predicate.IsAliasWithColumnExpression()
                        || selectExpression.Predicate is SelectExpression)
                    {
                        CommandBuilder.Append(" = ");
                        CommandBuilder.Append(TrueLiteral);
                    }
                }
            }

            if (selectExpression.OrderBy.Any())
            {
                CommandBuilder.AppendLine()
                    .Append("ORDER BY ");

                VisitJoin(selectExpression.OrderBy, t =>
                    {
                        var aliasExpression = t.Expression as AliasExpression;

                        if (aliasExpression != null)
                        {
                            if (aliasExpression.Alias != null)
                            {
                                var columnExpression = aliasExpression.TryGetColumnExpression();

                                if (columnExpression != null)
                                {
                                    CommandBuilder.Append(DelimitIdentifier(columnExpression.TableAlias))
                                        .Append(".");
                                }

                                CommandBuilder.Append(DelimitIdentifier(aliasExpression.Alias));
                            }
                            else
                            {
                                Visit(aliasExpression.Expression);
                            }
                        }
                        else
                        {
                            Visit(t.Expression);
                        }

                        if (t.OrderingDirection == OrderingDirection.Desc)
                        {
                            CommandBuilder.Append(" DESC");
                        }
                    });
            }

            GenerateLimitOffset(selectExpression);

            if (subQueryIndent != null)
            {
                subQueryIndent.Dispose();

                CommandBuilder.AppendLine()
                    .Append(")");

                if (selectExpression.Alias.Length > 0)
                {
                    CommandBuilder.Append(" AS ")
                        .Append(DelimitIdentifier(selectExpression.Alias));
                }
            }

            return selectExpression;
        }

        private void VisitJoin(
            IReadOnlyList<Expression> expressions, Action<RelationalCommandBuilder> joinAction = null)
            => VisitJoin(expressions, e => Visit(e), joinAction);

        private void VisitJoin<T>(
            IReadOnlyList<T> items, Action<T> itemAction, Action<RelationalCommandBuilder> joinAction = null)
        {
            joinAction = joinAction ?? (isb => isb.Append(", "));

            for (var i = 0; i < items.Count; i++)
            {
                if (i > 0)
                {
                    joinAction(CommandBuilder);
                }

                itemAction(items[i]);
            }
        }

        public virtual Expression VisitRawSqlDerivedTable(RawSqlDerivedTableExpression rawSqlDerivedTableExpression)
        {
            Check.NotNull(rawSqlDerivedTableExpression, nameof(rawSqlDerivedTableExpression));

            CommandBuilder.AppendLine("(");

            using (CommandBuilder.Indent())
            {

                CommandBuilder.AppendFormatLines(
                    rawSqlDerivedTableExpression.Sql,
                    rawSqlDerivedTableExpression.Parameters);
            }

            CommandBuilder.Append(") AS ")
                .Append(DelimitIdentifier(rawSqlDerivedTableExpression.Alias));

            return rawSqlDerivedTableExpression;
        }

        public virtual Expression VisitTable(TableExpression tableExpression)
        {
            Check.NotNull(tableExpression, nameof(tableExpression));

            if (tableExpression.Schema != null)
            {
                CommandBuilder.Append(DelimitIdentifier(tableExpression.Schema))
                    .Append(".");
            }

            CommandBuilder.Append(DelimitIdentifier(tableExpression.Table))
                .Append(" AS ")
                .Append(DelimitIdentifier(tableExpression.Alias));

            return tableExpression;
        }

        public virtual Expression VisitCrossJoin(CrossJoinExpression crossJoinExpression)
        {
            Check.NotNull(crossJoinExpression, nameof(crossJoinExpression));

            CommandBuilder.Append("CROSS JOIN ");

            Visit(crossJoinExpression.TableExpression);

            return crossJoinExpression;
        }

        public virtual Expression VisitCount(CountExpression countExpression)
        {
            Check.NotNull(countExpression, nameof(countExpression));

            CommandBuilder.Append("COUNT(*)");

            return countExpression;
        }

        public virtual Expression VisitSum(SumExpression sumExpression)
        {
            Check.NotNull(sumExpression, nameof(sumExpression));

            CommandBuilder.Append("SUM(");

            Visit(sumExpression.Expression);

            CommandBuilder.Append(")");

            return sumExpression;
        }

        public virtual Expression VisitMin(MinExpression minExpression)
        {
            Check.NotNull(minExpression, nameof(minExpression));

            CommandBuilder.Append("MIN(");

            Visit(minExpression.Expression);

            CommandBuilder.Append(")");

            return minExpression;
        }

        public virtual Expression VisitMax(MaxExpression maxExpression)
        {
            Check.NotNull(maxExpression, nameof(maxExpression));

            CommandBuilder.Append("MAX(");

            Visit(maxExpression.Expression);

            CommandBuilder.Append(")");

            return maxExpression;
        }

        public virtual Expression VisitIn(InExpression inExpression)
        {
            if (inExpression.Values != null)
            {
                var inValues = ProcessInExpressionValues(inExpression.Values);
                var inValuesNotNull = ExtractNonNullExpressionValues(inValues);

                if (inValues.Count != inValuesNotNull.Count)
                {
                    var nullSemanticsInExpression = Expression.OrElse(
                        new InExpression(inExpression.Operand, inValuesNotNull),
                        new IsNullExpression(inExpression.Operand));

                    return Visit(nullSemanticsInExpression);
                }

                if (inValuesNotNull.Count > 0)
                {
                    Visit(inExpression.Operand);

                    CommandBuilder.Append(" IN (");

                    VisitJoin(inValuesNotNull);

                    CommandBuilder.Append(")");
                }
                else
                {
                    CommandBuilder.Append("1 = 0");
                }
            }
            else
            {
                Visit(inExpression.Operand);

                CommandBuilder.Append(" IN ");

                Visit(inExpression.SubQuery);
            }

            return inExpression;
        }

        protected virtual Expression VisitNotIn(InExpression inExpression)
        {
            if (inExpression.Values != null)
            {
                var inValues = ProcessInExpressionValues(inExpression.Values);
                var inValuesNotNull = ExtractNonNullExpressionValues(inValues);

                if (inValues.Count != inValuesNotNull.Count)
                {
                    var nullSemanticsNotInExpression = Expression.AndAlso(
                        Expression.Not(new InExpression(inExpression.Operand, inValuesNotNull)),
                        Expression.Not(new IsNullExpression(inExpression.Operand)));

                    return Visit(nullSemanticsNotInExpression);
                }

                if (inValues.Count > 0)
                {
                    Visit(inExpression.Operand);

                    CommandBuilder.Append(" NOT IN (");

                    VisitJoin(inValues);

                    CommandBuilder.Append(")");
                }
                else
                {
                    CommandBuilder.Append("1 = 1");
                }
            }
            else
            {
                Visit(inExpression.Operand);

                CommandBuilder.Append(" NOT IN ");

                Visit(inExpression.SubQuery);
            }

            return inExpression;
        }

        protected virtual IReadOnlyList<Expression> ProcessInExpressionValues(
            [NotNull] IReadOnlyList<Expression> inExpressionValues)
        {
            Check.NotNull(inExpressionValues, nameof(inExpressionValues));

            var inConstants = new List<Expression>();

            foreach (var inValue in inExpressionValues)
            {
                var inConstant = inValue as ConstantExpression;
                if (inConstant != null)
                {
                    inConstants.Add(inConstant);
                    continue;
                }

                var inParameter = inValue as ParameterExpression;
                if (inParameter != null)
                {
                    object parameterValue;
                    if (_parameterValues.TryGetValue(inParameter.Name, out parameterValue))
                    {
                        var valuesCollection = parameterValue as IEnumerable;

                        if (valuesCollection != null
                            && parameterValue.GetType() != typeof(string)
                            && parameterValue.GetType() != typeof(byte[]))
                        {
                            inConstants.AddRange(valuesCollection.Cast<object>().Select(Expression.Constant));
                        }
                        else
                        {
                            inConstants.Add(inParameter);
                        }
                    }
                }
            }

            return inConstants;
        }

        protected virtual IReadOnlyList<Expression> ExtractNonNullExpressionValues(
            IReadOnlyList<Expression> inExpressionValues)
        {
            var inValuesNotNull = new List<Expression>();
            foreach (var inValue in inExpressionValues)
            {
                var inConstant = inValue as ConstantExpression;
                if (inConstant?.Value != null)
                {
                    inValuesNotNull.Add(inValue);
                    continue;
                }

                var inParameter = inValue as ParameterExpression;
                if (inParameter != null)
                {
                    object parameterValue;
                    if (_parameterValues.TryGetValue(inParameter.Name, out parameterValue))
                    {
                        if (parameterValue != null)
                        {
                            inValuesNotNull.Add(inValue);
                        }
                    }
                }
            }

            return inValuesNotNull;
        }

        public virtual Expression VisitInnerJoin(InnerJoinExpression innerJoinExpression)
        {
            Check.NotNull(innerJoinExpression, nameof(innerJoinExpression));

            CommandBuilder.Append("INNER JOIN ");

            Visit(innerJoinExpression.TableExpression);

            CommandBuilder.Append(" ON ");

            Visit(innerJoinExpression.Predicate);

            return innerJoinExpression;
        }

        public virtual Expression VisitOuterJoin(LeftOuterJoinExpression leftOuterJoinExpression)
        {
            Check.NotNull(leftOuterJoinExpression, nameof(leftOuterJoinExpression));

            CommandBuilder.Append("LEFT JOIN ");

            Visit(leftOuterJoinExpression.TableExpression);

            CommandBuilder.Append(" ON ");

            Visit(leftOuterJoinExpression.Predicate);

            return leftOuterJoinExpression;
        }

        protected virtual void GenerateTop([NotNull] SelectExpression selectExpression)
        {
            Check.NotNull(selectExpression, nameof(selectExpression));

            if (selectExpression.Limit != null
                && selectExpression.Offset == null)
            {
                CommandBuilder.Append("TOP(")
                    .Append(selectExpression.Limit)
                    .Append(") ");
            }
        }

        protected virtual void GenerateLimitOffset([NotNull] SelectExpression selectExpression)
        {
            Check.NotNull(selectExpression, nameof(selectExpression));

            if (selectExpression.Offset != null)
            {
                CommandBuilder.AppendLine()
                    .Append("OFFSET ")
                    .Append(selectExpression.Offset)
                    .Append(" ROWS");

                if (selectExpression.Limit != null)
                {
                    CommandBuilder.Append(" FETCH NEXT ")
                        .Append(selectExpression.Limit)
                        .Append(" ROWS ONLY");
                }
            }
        }

        protected override Expression VisitConditional(ConditionalExpression expression)
        {
            Check.NotNull(expression, nameof(expression));

            CommandBuilder.AppendLine("CASE");

            using (CommandBuilder.Indent())
            {
                CommandBuilder.AppendLine("WHEN");

                using (CommandBuilder.Indent())
                {
                    CommandBuilder.Append("(");

                    Visit(expression.Test);

                    CommandBuilder.AppendLine(")");
                }

                CommandBuilder.Append("THEN ");

                var constantIfTrue = expression.IfTrue as ConstantExpression;

                if (constantIfTrue != null
                    && constantIfTrue.Type == typeof(bool))
                {
                    CommandBuilder.Append((bool)constantIfTrue.Value ? TypedTrueLiteral : TypedFalseLiteral);
                }
                else
                {
                    Visit(expression.IfTrue);
                }

                CommandBuilder.Append(" ELSE ");

                var constantIfFalse = expression.IfFalse as ConstantExpression;

                if (constantIfFalse != null
                    && constantIfFalse.Type == typeof(bool))
                {
                    CommandBuilder.Append((bool)constantIfFalse.Value ? TypedTrueLiteral : TypedFalseLiteral);
                }
                else
                {
                    Visit(expression.IfFalse);
                }

                CommandBuilder.AppendLine();
            }

            CommandBuilder.Append("END");

            return expression;
        }

        public virtual Expression VisitExists(ExistsExpression existsExpression)
        {
            Check.NotNull(existsExpression, nameof(existsExpression));

            CommandBuilder.AppendLine("EXISTS (");

            using (CommandBuilder.Indent())
            {
                Visit(existsExpression.Expression);
            }

            CommandBuilder.AppendLine(")");

            return existsExpression;
        }

        protected override Expression VisitBinary(BinaryExpression binaryExpression)
        {
            Check.NotNull(binaryExpression, nameof(binaryExpression));

            if (binaryExpression.NodeType == ExpressionType.Coalesce)
            {
                CommandBuilder.Append("COALESCE(");
                Visit(binaryExpression.Left);
                CommandBuilder.Append(", ");
                Visit(binaryExpression.Right);
                CommandBuilder.Append(")");
            }
            else
            {
                var needParentheses
                    = !binaryExpression.Left.IsSimpleExpression()
                      || !binaryExpression.Right.IsSimpleExpression()
                      || binaryExpression.IsLogicalOperation();

                if (needParentheses)
                {
                    CommandBuilder.Append("(");
                }

                Visit(binaryExpression.Left);

                if (binaryExpression.IsLogicalOperation()
                    && binaryExpression.Left.IsSimpleExpression())
                {
                    CommandBuilder.Append(" = ");
                    CommandBuilder.Append(TrueLiteral);
                }

                string op;

                switch (binaryExpression.NodeType)
                {
                    case ExpressionType.Equal:
                        op = " = ";
                        break;
                    case ExpressionType.NotEqual:
                        op = " <> ";
                        break;
                    case ExpressionType.GreaterThan:
                        op = " > ";
                        break;
                    case ExpressionType.GreaterThanOrEqual:
                        op = " >= ";
                        break;
                    case ExpressionType.LessThan:
                        op = " < ";
                        break;
                    case ExpressionType.LessThanOrEqual:
                        op = " <= ";
                        break;
                    case ExpressionType.AndAlso:
                        op = " AND ";
                        break;
                    case ExpressionType.OrElse:
                        op = " OR ";
                        break;
                    case ExpressionType.Add:
                        op = (binaryExpression.Left.Type == typeof(string)
                              && binaryExpression.Right.Type == typeof(string))
                            ? " " + ConcatOperator + " "
                            : " + ";
                        break;
                    case ExpressionType.Subtract:
                        op = " - ";
                        break;
                    case ExpressionType.Multiply:
                        op = " * ";
                        break;
                    case ExpressionType.Divide:
                        op = " / ";
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                CommandBuilder.Append(op);

                Visit(binaryExpression.Right);

                if (binaryExpression.IsLogicalOperation()
                    && binaryExpression.Right.IsSimpleExpression())
                {
                    CommandBuilder.Append(" = ");
                    CommandBuilder.Append(TrueLiteral);
                }

                if (needParentheses)
                {
                    CommandBuilder.Append(")");
                }
            }

            return binaryExpression;
        }

        public virtual Expression VisitColumn(ColumnExpression columnExpression)
        {
            Check.NotNull(columnExpression, nameof(columnExpression));

            CommandBuilder.Append(DelimitIdentifier(columnExpression.TableAlias))
                .Append(".")
                .Append(DelimitIdentifier(columnExpression.Name));

            return columnExpression;
        }

        public virtual Expression VisitAlias(AliasExpression aliasExpression)
        {
            Check.NotNull(aliasExpression, nameof(aliasExpression));

            if (!aliasExpression.Projected)
            {
                Visit(aliasExpression.Expression);

                if (aliasExpression.Alias != null)
                {
                    CommandBuilder.Append(" AS ");
                }
            }

            if (aliasExpression.Alias != null)
            {
                CommandBuilder.Append(DelimitIdentifier(aliasExpression.Alias));
            }

            return aliasExpression;
        }

        public virtual Expression VisitIsNull(IsNullExpression isNullExpression)
        {
            Check.NotNull(isNullExpression, nameof(isNullExpression));

            Visit(isNullExpression.Operand);

            CommandBuilder.Append(" IS NULL");

            return isNullExpression;
        }

        public virtual Expression VisitIsNotNull([NotNull] IsNullExpression isNotNullExpression)
        {
            Check.NotNull(isNotNullExpression, nameof(isNotNullExpression));

            Visit(isNotNullExpression.Operand);

            CommandBuilder.Append(" IS NOT NULL");

            return isNotNullExpression;
        }

        public virtual Expression VisitLike(LikeExpression likeExpression)
        {
            Check.NotNull(likeExpression, nameof(likeExpression));

            Visit(likeExpression.Match);

            CommandBuilder.Append(" LIKE ");

            Visit(likeExpression.Pattern);

            return likeExpression;
        }

        public virtual Expression VisitLiteral(LiteralExpression literalExpression)
        {
            Check.NotNull(literalExpression, nameof(literalExpression));

            CommandBuilder.Append(GenerateLiteral(literalExpression.Literal));

            return literalExpression;
        }

        public virtual Expression VisitSqlFunction(SqlFunctionExpression sqlFunctionExpression)
        {
            CommandBuilder.Append(sqlFunctionExpression.FunctionName);
            CommandBuilder.Append("(");

            VisitJoin(sqlFunctionExpression.Arguments.ToList());

            CommandBuilder.Append(")");

            return sqlFunctionExpression;
        }

        protected override Expression VisitUnary(UnaryExpression unaryExpression)
        {
            Check.NotNull(unaryExpression, nameof(unaryExpression));

            if (unaryExpression.NodeType == ExpressionType.Not)
            {
                var inExpression = unaryExpression.Operand as InExpression;
                if (inExpression != null)
                {
                    return VisitNotIn(inExpression);
                }

                var isNullExpression = unaryExpression.Operand as IsNullExpression;
                if (isNullExpression != null)
                {
                    return VisitIsNotNull(isNullExpression);
                }

                var isColumnOrParameterOperand =
                    unaryExpression.Operand is ColumnExpression
                    || unaryExpression.Operand is ParameterExpression
                    || unaryExpression.Operand.IsAliasWithColumnExpression();

                if (!isColumnOrParameterOperand)
                {
                    CommandBuilder.Append("NOT (");
                    Visit(unaryExpression.Operand);
                    CommandBuilder.Append(")");
                }
                else
                {
                    Visit(unaryExpression.Operand);
                    CommandBuilder.Append(" = ");
                    CommandBuilder.Append(FalseLiteral);
                }

                return unaryExpression;
            }

            if (unaryExpression.NodeType == ExpressionType.Convert)
            {
                Visit(unaryExpression.Operand);

                return unaryExpression;
            }

            return base.VisitUnary(unaryExpression);
        }

        protected override Expression VisitConstant(ConstantExpression constantExpression)
        {
            Check.NotNull(constantExpression, nameof(constantExpression));

            CommandBuilder.Append(constantExpression.Value == null
                ? "NULL"
                : GenerateLiteral((dynamic)constantExpression.Value));

            return constantExpression;
        }

        protected override Expression VisitParameter(ParameterExpression parameterExpression)
        {
            Check.NotNull(parameterExpression, nameof(parameterExpression));

            object value;
            if (!_parameterValues.TryGetValue(parameterExpression.Name, out value))
            {
                value = string.Empty;
            }

            CommandBuilder.AppendNamedParameter(
                parameterExpression.Name,
                value);

            return parameterExpression;
        }

        protected override Exception CreateUnhandledItemException<T>(T unhandledItem, string visitMethod)
            => new NotImplementedException(visitMethod);

        // TODO: Share the code below (#1559)

        private const string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffK";
        private const string DateTimeOffsetFormat = "yyyy-MM-ddTHH:mm:ss.fffzzz";
        private const string FloatingPointFormat = "{0}E0";

        protected virtual string GenerateLiteral([NotNull] object value)
            => string.Format(CultureInfo.InvariantCulture, "{0}", value);

        protected virtual string GenerateLiteral([NotNull] Enum value)
            => string.Format(CultureInfo.InvariantCulture, "{0:d}", value);


        private readonly Dictionary<DbType, string> _dbTypeNameMapping = new Dictionary<DbType, string>
        {
            { DbType.Byte, "tinyint" },
            { DbType.Decimal, "decimal" },
            { DbType.Double, "float" },
            { DbType.Int16, "smallint" },
            { DbType.Int32, "int" },
            { DbType.Int64, "bigint" },
            { DbType.String, "nvarchar" },
        };

        protected virtual string GenerateLiteral(DbType value)
            => _dbTypeNameMapping[value];

        protected virtual string GenerateLiteral(int value)
            => value.ToString();

        protected virtual string GenerateLiteral(short value)
            => value.ToString();

        protected virtual string GenerateLiteral(long value)
            => value.ToString();

        protected virtual string GenerateLiteral(byte value)
            => value.ToString();

        protected virtual string GenerateLiteral(decimal value)
            => string.Format(value.ToString(CultureInfo.InvariantCulture));

        protected virtual string GenerateLiteral(double value)
            => string.Format(CultureInfo.InvariantCulture, FloatingPointFormat, value);

        protected virtual string GenerateLiteral(float value)
            => string.Format(CultureInfo.InvariantCulture, FloatingPointFormat, value);

        protected virtual string GenerateLiteral(bool value)
            => value ? TrueLiteral : FalseLiteral;

        protected virtual string GenerateLiteral([NotNull] string value)
            => "'" + EscapeLiteral(Check.NotNull(value, nameof(value))) + "'";

        protected virtual string GenerateLiteral(Guid value)
            => "'" + value + "'";

        protected virtual string GenerateLiteral(DateTime value)
            => "'" + value.ToString(DateTimeFormat, CultureInfo.InvariantCulture) + "'";

        protected virtual string GenerateLiteral(DateTimeOffset value)
            => "'" + value.ToString(DateTimeOffsetFormat, CultureInfo.InvariantCulture) + "'";

        protected virtual string GenerateLiteral(TimeSpan value)
            => "'" + value + "'";

        protected virtual string GenerateLiteral([NotNull] byte[] value)
        {
            var stringBuilder = new StringBuilder("0x");

            foreach (var @byte in value)
            {
                stringBuilder.Append(@byte.ToString("X2", CultureInfo.InvariantCulture));
            }

            return stringBuilder.ToString();
        }

        protected virtual string EscapeLiteral([NotNull] string literal)
            => Check.NotNull(literal, nameof(literal)).Replace("'", "''");

        protected virtual string DelimitIdentifier([NotNull] string identifier)
            => "\"" + Check.NotEmpty(identifier, nameof(identifier)) + "\"";

        private class NullComparisonTransformingVisitor : RelinqExpressionVisitor
        {
            private readonly IDictionary<string, object> _parameterValues;

            public NullComparisonTransformingVisitor(IDictionary<string, object> parameterValues)
            {
                _parameterValues = parameterValues;
            }

            protected override Expression VisitBinary(BinaryExpression expression)
            {
                if (expression.NodeType == ExpressionType.Equal
                    || expression.NodeType == ExpressionType.NotEqual)
                {
                    var parameter
                        = expression.Right as ParameterExpression
                          ?? expression.Left as ParameterExpression;

                    object parameterValue;
                    if (parameter != null
                        && _parameterValues.TryGetValue(parameter.Name, out parameterValue)
                        && parameterValue == null)
                    {
                        var columnExpression
                            = expression.Left.RemoveConvert().TryGetColumnExpression()
                              ?? expression.Right.RemoveConvert().TryGetColumnExpression();

                        if (columnExpression != null)
                        {
                            return
                                expression.NodeType == ExpressionType.Equal
                                    ? (Expression)new IsNullExpression(columnExpression)
                                    : Expression.Not(new IsNullExpression(columnExpression));
                        }
                    }
                }

                return base.VisitBinary(expression);
            }
        }
    }
}
