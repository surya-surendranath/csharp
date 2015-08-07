// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Commands.Utilities;
using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Data.Entity.Internal;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Utilities;

namespace Microsoft.Data.Entity.Commands.Migrations
{
    public class CSharpModelGenerator
    {
        private readonly CSharpHelper _code;

        public CSharpModelGenerator([NotNull] CSharpHelper code)
        {
            Check.NotNull(code, nameof(code));

            _code = code;
        }

        public virtual void Generate([NotNull] IModel model, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(model, nameof(model));
            Check.NotNull(stringBuilder, nameof(stringBuilder));

            var annotations = model.Annotations.ToArray();
            if (annotations.Length != 0)
            {
                stringBuilder.Append("builder");

                using (stringBuilder.Indent())
                {
                    GenerateAnnotations(annotations, stringBuilder);
                }

                stringBuilder.AppendLine(";");
            }

            GenerateEntityTypes(Sort(model.EntityTypes), stringBuilder);
        }

        [Flags]
        protected enum GenerateEntityTypeOptions
        {
            Declared = 1,
            Relationships = 2,
            Full = Declared | Relationships
        }

        private IReadOnlyList<IEntityType> Sort(IReadOnlyList<IEntityType> entityTypes)
        {
            var entityTypeGraph = new Multigraph<IEntityType, int>();
            entityTypeGraph.AddVertices(entityTypes);
            foreach (var entityType in entityTypes.Where(et => et.BaseType != null))
            {
                entityTypeGraph.AddEdge(entityType.BaseType, entityType, 0);
            }
            return entityTypeGraph.TopologicalSort();
        }

        protected virtual void GenerateEntityTypes(
            IReadOnlyList<IEntityType> entityTypes, IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(entityTypes, nameof(entityTypes));
            Check.NotNull(stringBuilder, nameof(stringBuilder));

            foreach (var entityType in entityTypes)
            {
                stringBuilder.AppendLine();

                GenerateEntityType(entityType, stringBuilder, GenerateEntityTypeOptions.Declared);
            }

            foreach (var entityType in entityTypes.Where(e => e.GetForeignKeys().Any()))
            {
                stringBuilder.AppendLine();

                GenerateEntityType(entityType, stringBuilder, GenerateEntityTypeOptions.Relationships);
            }
        }

        protected virtual void GenerateEntityType(
            [NotNull] IEntityType entityType, [NotNull] IndentedStringBuilder stringBuilder, GenerateEntityTypeOptions options)
        {
            Check.NotNull(entityType, nameof(entityType));
            Check.NotNull(stringBuilder, nameof(stringBuilder));

            stringBuilder
                .Append("builder.Entity(")
                .Append(_code.Literal(entityType.Name))
                .AppendLine(", b =>");

            using (stringBuilder.Indent())
            {
                stringBuilder.Append("{");

                using (stringBuilder.Indent())
                {
                    if ((options & GenerateEntityTypeOptions.Declared) != 0)
                    {
                        GenerateBaseType(entityType.BaseType, stringBuilder);

                        GenerateProperties(entityType.GetDeclaredProperties(), stringBuilder);

                        GenerateKeys(entityType.GetDeclaredKeys(), entityType.FindDeclaredPrimaryKey(), stringBuilder);

                        GenerateIndexes(entityType.GetDeclaredIndexes(), stringBuilder);
                    }

                    if ((options & GenerateEntityTypeOptions.Relationships) != 0)
                    {
                        GenerateForeignKeys(entityType.GetDeclaredForeignKeys(), stringBuilder);
                    }

                    if ((options & GenerateEntityTypeOptions.Declared) != 0)
                    {
                        GenerateEntityTypeAnnotations(entityType, stringBuilder);
                    }
                }

                stringBuilder
                    .AppendLine()
                    .AppendLine("});");
            }
        }

        protected virtual void GenerateBaseType([CanBeNull] IEntityType baseType, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(stringBuilder, nameof(stringBuilder));

            if (baseType != null)
            {
                stringBuilder
                    .AppendLine()
                    .Append("b.BaseType(")
                    .Append(_code.Literal(baseType.Name))
                    .AppendLine(");");
            }
        }

        protected virtual void GenerateProperties(
            [NotNull] IEnumerable<IProperty> properties, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(properties, nameof(properties));
            Check.NotNull(stringBuilder, nameof(stringBuilder));

            var firstProperty = true;
            foreach (var property in properties)
            {
                if (!firstProperty)
                {
                    stringBuilder.AppendLine();
                }
                else
                {
                    firstProperty = false;
                }

                GenerateProperty(property, stringBuilder);
            }
        }

        protected virtual void GenerateProperty(
            [NotNull] IProperty property, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(property, nameof(property));
            Check.NotNull(stringBuilder, nameof(stringBuilder));

            stringBuilder
                .AppendLine()
                .Append("b.Property<")
                .Append(_code.Reference(property.ClrType.UnwrapEnumType()))
                .Append(">(")
                .Append(_code.Literal(property.Name))
                .Append(")");

            using (stringBuilder.Indent())
            {
                if (property.IsConcurrencyToken)
                {
                    stringBuilder
                        .AppendLine()
                        .Append(".ConcurrencyToken()");
                }

                if (property.IsNullable != (property.ClrType.IsNullableType() && !property.IsPrimaryKey()))
                {
                    stringBuilder
                        .AppendLine()
                        .Append(".Required()");
                }

                if (property.ValueGenerated != ValueGenerated.Never)
                {
                    stringBuilder
                        .AppendLine()
                        .Append(
                            property.ValueGenerated == ValueGenerated.OnAdd
                                ? ".ValueGeneratedOnAdd()"
                                : ".ValueGeneratedOnAddOrUpdate()");
                }

                GeneratePropertyAnnotations(property, stringBuilder);
            }

            stringBuilder.Append(";");
        }

        protected virtual void GeneratePropertyAnnotations([NotNull] IProperty property, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(property, nameof(property));
            Check.NotNull(stringBuilder, nameof(stringBuilder));

            GenerateAnnotations(property.Annotations.ToArray(), stringBuilder);
        }

        protected virtual void GenerateKeys(
            [NotNull] IEnumerable<IKey> keys, [CanBeNull] IKey primaryKey, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(keys, nameof(keys));
            Check.NotNull(stringBuilder, nameof(stringBuilder));

            if (primaryKey != null)
            {
                GenerateKey(primaryKey, stringBuilder, primary: true);
            }

            var firstKey = true;
            foreach (var key in keys.Where(key => key != primaryKey && !key.EntityType.Model.GetReferencingForeignKeys(key).Any()))
            {
                if (!firstKey)
                {
                    stringBuilder.AppendLine();
                }
                else
                {
                    firstKey = false;
                }

                GenerateKey(key, stringBuilder, primary: false);
            }
        }

        protected virtual void GenerateKey(
            [NotNull] IKey key, [NotNull] IndentedStringBuilder stringBuilder, bool primary = false)
        {
            Check.NotNull(stringBuilder, nameof(stringBuilder));

            stringBuilder
                .AppendLine()
                .AppendLine()
                .Append(primary ? "b.Key(" : "b.AlternateKey(")
                .Append(string.Join(", ", key.Properties.Select(p => _code.Literal(p.Name))))
                .Append(")");

            using (stringBuilder.Indent())
            {
                GenerateAnnotations(key.Annotations.ToArray(), stringBuilder);
            }

            stringBuilder.Append(";");
        }

        protected virtual void GenerateIndexes(
            [NotNull] IEnumerable<IIndex> indexes, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(indexes, nameof(indexes));
            Check.NotNull(stringBuilder, nameof(stringBuilder));

            foreach (var index in indexes)
            {
                stringBuilder.AppendLine();
                GenerateIndex(index, stringBuilder);
            }
        }

        protected virtual void GenerateIndex(
            [NotNull] IIndex index, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(index, nameof(index));
            Check.NotNull(stringBuilder, nameof(stringBuilder));

            stringBuilder
                .AppendLine()
                .Append("b.Index(")
                .Append(string.Join(", ", index.Properties.Select(p => _code.Literal(p.Name))))
                .Append(")");

            using (stringBuilder.Indent())
            {
                if (index.IsUnique)
                {
                    stringBuilder
                        .AppendLine()
                        .Append(".Unique()");
                }

                GenerateAnnotations(index.Annotations.ToArray(), stringBuilder);
            }

            stringBuilder.Append(";");
        }

        protected virtual void GenerateEntityTypeAnnotations([NotNull] IEntityType entityType, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(entityType, nameof(entityType));
            Check.NotNull(stringBuilder, nameof(stringBuilder));

            var annotations = entityType.Annotations.ToArray();
            if (annotations.Any())
            {
                foreach (var annotation in annotations)
                {
                    stringBuilder
                        .AppendLine()
                        .AppendLine()
                        .Append("b");

                    GenerateAnnotation(annotation, stringBuilder);

                    stringBuilder.Append(";");
                }
            }
        }

        protected virtual void GenerateForeignKeys(
            [NotNull] IEnumerable<IForeignKey> foreignKeys, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(foreignKeys, nameof(foreignKeys));
            Check.NotNull(stringBuilder, nameof(stringBuilder));

            var firstForeignKey = true;
            foreach (var foreignKey in foreignKeys)
            {
                if (!firstForeignKey)
                {
                    stringBuilder.AppendLine();
                }
                else
                {
                    firstForeignKey = false;
                }

                GenerateForeignKey(foreignKey, stringBuilder);
            }
        }

        protected virtual void GenerateForeignKey(
            [NotNull] IForeignKey foreignKey, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(foreignKey, nameof(foreignKey));
            Check.NotNull(stringBuilder, nameof(stringBuilder));

            stringBuilder
                .AppendLine()
                .Append("b.Reference(")
                .Append(_code.Literal(foreignKey.PrincipalEntityType.Name))
                .Append(")")
                .AppendLine();

            using (stringBuilder.Indent())
            {
                if (foreignKey.IsUnique)
                {
                    stringBuilder
                        .AppendLine(".InverseReference()")
                        .Append(".ForeignKey(")
                        .Append(_code.Literal(foreignKey.DeclaringEntityType.Name))
                        .Append(", ")
                        .Append(string.Join(", ", foreignKey.Properties.Select(p => _code.Literal(p.Name))))
                        .Append(")");

                    GenerateForeignKeyAnnotations(foreignKey, stringBuilder);

                    if (foreignKey.PrincipalKey != foreignKey.PrincipalEntityType.GetPrimaryKey())
                    {
                        stringBuilder
                            .AppendLine()
                            .Append(".PrincipalKey(")
                            .Append(_code.Literal(foreignKey.PrincipalEntityType.Name))
                            .Append(", ")
                            .Append(string.Join(", ", foreignKey.PrincipalKey.Properties.Select(p => _code.Literal(p.Name))))
                            .Append(")");
                    }
                }
                else
                {
                    stringBuilder
                        .AppendLine(".InverseCollection()")
                        .Append(".ForeignKey(")
                        .Append(string.Join(", ", foreignKey.Properties.Select(p => _code.Literal(p.Name))))
                        .Append(")");

                    GenerateForeignKeyAnnotations(foreignKey, stringBuilder);

                    if (foreignKey.PrincipalKey != foreignKey.PrincipalEntityType.GetPrimaryKey())
                    {
                        stringBuilder
                            .AppendLine()
                            .Append(".PrincipalKey(")
                            .Append(string.Join(", ", foreignKey.PrincipalKey.Properties.Select(p => _code.Literal(p.Name))))
                            .Append(")");
                    }
                }
            }

            stringBuilder.Append(";");
        }

        protected virtual void GenerateForeignKeyAnnotations([NotNull] IForeignKey foreignKey, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(foreignKey, nameof(foreignKey));
            Check.NotNull(stringBuilder, nameof(stringBuilder));

            GenerateAnnotations(foreignKey.Annotations.ToArray(), stringBuilder);
        }

        protected virtual void GenerateAnnotations(
            [NotNull] IReadOnlyList<IAnnotation> annotations, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(annotations, nameof(annotations));
            Check.NotNull(stringBuilder, nameof(stringBuilder));

            foreach (var annotation in annotations.Where(annotation => !MigrationCodeGenerator.IgnoredAnnotations.Contains(annotation.Name)))
            {
                stringBuilder.AppendLine();
                GenerateAnnotation(annotation, stringBuilder);
            }
        }

        protected virtual void GenerateAnnotation(
            [NotNull] IAnnotation annotation, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(annotation, nameof(annotation));
            Check.NotNull(stringBuilder, nameof(stringBuilder));

            stringBuilder
                .Append(".Annotation(")
                .Append(_code.Literal(annotation.Name))
                .Append(", ")
                .Append(_code.UnknownLiteral(annotation.Value))
                .Append(")");
        }
    }
}
