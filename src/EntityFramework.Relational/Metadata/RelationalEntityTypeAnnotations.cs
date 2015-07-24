// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Relational.Internal;
using Microsoft.Data.Entity.Utilities;

namespace Microsoft.Data.Entity.Metadata
{
    public class RelationalEntityTypeAnnotations : IRelationalEntityTypeAnnotations
    {
        public RelationalEntityTypeAnnotations([NotNull] IEntityType entityType, [CanBeNull] string providerPrefix)
            : this(new RelationalAnnotations(entityType, providerPrefix))
        {
        }

        protected RelationalEntityTypeAnnotations([NotNull] RelationalAnnotations annotations)
        {
            Annotations = annotations;
        }

        protected RelationalAnnotations Annotations { get; }

        protected virtual IEntityType EntityType => (IEntityType)Annotations.Metadata;

        public virtual string TableName
        {
            get
            {
                var rootType = EntityType.RootType();
                var rootAnnotations = new RelationalAnnotations(rootType, Annotations.ProviderPrefix);

                return (string)rootAnnotations.GetAnnotation(RelationalAnnotationNames.TableName)
                       ?? rootType.DisplayName();
            }
            [param: CanBeNull] set { SetTableName(value); }
        }

        protected virtual bool SetTableName(string value)
            => Annotations.SetAnnotation(RelationalAnnotationNames.TableName, Check.NullButNotEmpty(value, nameof(value)));

        public virtual string Schema
        {
            get
            {
                var rootAnnotations = new RelationalAnnotations(EntityType.RootType(), Annotations.ProviderPrefix);
                return (string)rootAnnotations.GetAnnotation(RelationalAnnotationNames.Schema);
            }
            [param: CanBeNull] set { SetSchema(value); }
        }

        protected virtual bool SetSchema(string value)
            => Annotations.SetAnnotation(RelationalAnnotationNames.Schema, Check.NullButNotEmpty(value, nameof(value)));

        public virtual IProperty DiscriminatorProperty
        {
            get
            {
                var rootType = EntityType.RootType();
                var rootAnnotations = new RelationalAnnotations(rootType, Annotations.ProviderPrefix);
                var propertyName = (string)rootAnnotations.GetAnnotation(RelationalAnnotationNames.DiscriminatorProperty);

                return propertyName == null ? null : rootType.GetProperty(propertyName);
            }
            [param: CanBeNull] set { SetDiscriminatorProperty(value); }
        }

        protected virtual bool SetDiscriminatorProperty([CanBeNull] IProperty value)
        {
            if (value != null)
            {
                EnsureCanSetDiscriminator();

                if (value.DeclaringEntityType != EntityType)
                {
                    throw new InvalidOperationException(
                        Strings.DiscriminatorPropertyNotFound(value, EntityType));
                }
            }

            return Annotations.SetAnnotation(RelationalAnnotationNames.DiscriminatorProperty, value?.Name);
        }

        protected virtual void EnsureCanSetDiscriminator()
        {
            if (EntityType != EntityType.RootType())
            {
                throw new InvalidOperationException(
                    Strings.DiscriminatorPropertyMustBeOnRoot(EntityType));
            }
        }

        public virtual object DiscriminatorValue
        {
            get { return Annotations.GetAnnotation(RelationalAnnotationNames.DiscriminatorValue); }
            [param: CanBeNull] set { SetDiscriminatorValue(value); }
        }

        protected virtual bool SetDiscriminatorValue(object value)
        {
            if (DiscriminatorProperty == null)
            {
                throw new InvalidOperationException(
                    Strings.NoDiscriminator(EntityType.DisplayName(), EntityType.RootType().DisplayName()));
            }

            // ReSharper disable once UseMethodIsInstanceOfType
            if (value != null && !DiscriminatorProperty.ClrType.IsAssignableFrom(value.GetType()))
            {
                throw new InvalidOperationException(Strings.DiscriminitatorValueIncompatible(
                    value, DiscriminatorProperty.Name, DiscriminatorProperty.ClrType));
            }

            return Annotations.SetAnnotation(RelationalAnnotationNames.DiscriminatorValue, value);
        }
    }
}
