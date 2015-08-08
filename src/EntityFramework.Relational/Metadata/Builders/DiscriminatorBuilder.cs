// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Metadata.Internal;
using Microsoft.Data.Entity.Relational.Internal;

namespace Microsoft.Data.Entity.Metadata.Builders
{
    public class DiscriminatorBuilder
    {
        public DiscriminatorBuilder([NotNull] RelationalEntityTypeBuilderAnnotations annotationsBuilder)
        {
            AnnotationsBuilder = annotationsBuilder;
        }

        protected RelationalEntityTypeBuilderAnnotations AnnotationsBuilder { get; }
        
        public virtual DiscriminatorBuilder HasValue([NotNull] Type entityType, [CanBeNull] object value)
        {
            var entityTypeBuilder = AnnotationsBuilder.EntityTypeBuilder.ModelBuilder.Entity(entityType, ConfigurationSource.Convention);
            return HasValue(entityTypeBuilder, value);
        }

        public virtual DiscriminatorBuilder HasValue([NotNull] string entityTypeName, [CanBeNull] object value)
        {
            var entityTypeBuilder = AnnotationsBuilder.EntityTypeBuilder.ModelBuilder.Entity(entityTypeName, ConfigurationSource.Convention);
            return HasValue(entityTypeBuilder, value);
        }

        private DiscriminatorBuilder HasValue([NotNull] InternalEntityTypeBuilder entityTypeBuilder, [CanBeNull] object value)
        {
            var rootEntityTypeBuilder = AnnotationsBuilder.EntityTypeBuilder;
            if (!rootEntityTypeBuilder.Metadata.IsAssignableFrom(entityTypeBuilder.Metadata)
                && entityTypeBuilder.BaseType(rootEntityTypeBuilder.Metadata, AnnotationsBuilder.Annotations.ConfigurationSource) == null)
            {
                throw new InvalidOperationException(Strings.DiscriminatorEntityTypeNotDerived(
                    entityTypeBuilder.Metadata.DisplayName(),
                    rootEntityTypeBuilder.Metadata.DisplayName()));
            }

            var annotationsBuilder = rootEntityTypeBuilder == entityTypeBuilder ?
                AnnotationsBuilder
                : new RelationalEntityTypeBuilderAnnotations(
                    entityTypeBuilder,
                    AnnotationsBuilder.Annotations.ConfigurationSource,
                    AnnotationsBuilder.Annotations.ProviderPrefix);
            return annotationsBuilder.DiscriminatorValue(value) ? this : null;
        }
    }
}
