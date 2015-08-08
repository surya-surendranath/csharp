﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Data.Entity.FunctionalTests.TestModels.Inheritance;

namespace Microsoft.Data.Entity.FunctionalTests
{
    public abstract class InheritanceRelationalFixture : InheritanceFixtureBase
    {
        public override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // TODO: remove this when the discriminator convention is added
            modelBuilder.Entity<Animal>().Discriminator()
                .HasValue(typeof(Eagle), "Eagle")
                .HasValue(typeof(Kiwi), "Kiwi");

            modelBuilder.Entity<Plant>().Discriminator(p => p.Genus)
                .HasValue(typeof(Rose), PlantGenus.Rose)
                .HasValue(typeof(Daisy), PlantGenus.Daisy);
        }
    }
}
