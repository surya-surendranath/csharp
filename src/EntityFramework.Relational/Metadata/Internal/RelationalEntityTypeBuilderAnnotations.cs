// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Metadata.Builders;
using Microsoft.Data.Entity.Utilities;

namespace Microsoft.Data.Entity.Metadata.Internal
{
    public class RelationalEntityTypeBuilderAnnotations : RelationalEntityTypeAnnotations
    {
        public RelationalEntityTypeBuilderAnnotations(
            [NotNull] InternalEntityTypeBuilder internalBuilder,
            ConfigurationSource configurationSource,
            [CanBeNull] string providerPrefix)
            : base(new RelationalAnnotationsBuilder(internalBuilder, configurationSource, providerPrefix))
        {
        }

        public new virtual RelationalAnnotationsBuilder Annotations => (RelationalAnnotationsBuilder)base.Annotations;

        public virtual InternalEntityTypeBuilder EntityTypeBuilder => (InternalEntityTypeBuilder)Annotations.EntityTypeBuilder;

        public virtual bool ToTable([CanBeNull] string name)
        {
            Check.NullButNotEmpty(name, nameof(name));

            return SetTableName(name);
        }

        public virtual bool ToTable([CanBeNull] string name, [CanBeNull] string schema)
        {
            Check.NullButNotEmpty(name, nameof(name));
            Check.NullButNotEmpty(schema, nameof(schema));

            var originalTable = TableName;
            if (!SetTableName(name))
            {
                return false;
            }

            if (!SetSchema(schema))
            {
                SetTableName(originalTable);
                return false;
            }

            return true;
        }

        public virtual DiscriminatorBuilder Discriminator() => DiscriminatorBuilder(null);

        public virtual DiscriminatorBuilder Discriminator([NotNull] string name, [NotNull] Type discriminatorType)
            => DiscriminatorBuilder(b => b.Property(name, discriminatorType, Annotations.ConfigurationSource));

        public virtual DiscriminatorBuilder Discriminator([NotNull] PropertyInfo propertyInfo)
            => DiscriminatorBuilder(b => b.Property(propertyInfo, Annotations.ConfigurationSource));

        private DiscriminatorBuilder DiscriminatorBuilder(
            [CanBeNull] Func<InternalEntityTypeBuilder, InternalPropertyBuilder> createProperty)
        {
            EnsureCanSetDiscriminator();

            var discriminatorProperty = DiscriminatorProperty;
            if (discriminatorProperty != null
                && createProperty != null)
            {
                if (!SetDiscriminatorProperty(null))
                {
                    return null;
                }
            }

            InternalPropertyBuilder propertyBuilder;
            if (createProperty != null)
            {
                propertyBuilder = createProperty(EntityTypeBuilder);
            }
            else if (discriminatorProperty == null)
            {
                propertyBuilder = EntityTypeBuilder.Property(GetDefaultDiscriminatorName(), ConfigurationSource.Convention);
            }
            else
            {
                propertyBuilder = EntityTypeBuilder.Property(discriminatorProperty.Name, ConfigurationSource.Convention);
            }

            if (propertyBuilder == null)
            {
                if (discriminatorProperty != null
                    && createProperty != null)
                {
                    SetDiscriminatorProperty(discriminatorProperty);
                }
                return null;
            }

            var discriminatorSet = SetDiscriminatorProperty(propertyBuilder.Metadata);
            Debug.Assert(discriminatorSet);

            var configurationSource = (Annotations).ConfigurationSource;
            propertyBuilder.Required(true, configurationSource);
            //propertyBuilder.ReadOnlyBeforeSave(true, configurationSource);// #2132
            propertyBuilder.ReadOnlyAfterSave(true, configurationSource);
            propertyBuilder.UseValueGenerator(true, configurationSource);

            return new DiscriminatorBuilder(this);
        }

        public new virtual bool DiscriminatorValue([CanBeNull] object value) => SetDiscriminatorValue(value);

        protected virtual string GetDefaultDiscriminatorName() => "Discriminator";
    }
}
