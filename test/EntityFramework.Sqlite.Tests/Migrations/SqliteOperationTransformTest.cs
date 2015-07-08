// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Data.Entity.Migrations.Builders;
using Microsoft.Data.Entity.Migrations.Infrastructure;
using Microsoft.Data.Entity.Sqlite.Metadata;
using Microsoft.Data.Entity.Sqlite.Migrations.Operations;
using Xunit;

namespace Microsoft.Data.Entity.Sqlite.Migrations
{
    public class SqliteOperationTransformTest : SqliteOperationTransformBase
    {
        [Fact]
        public void AlterColumn_to_TableRebuild()
        {
            SimpleRebuildTest(migrate => { migrate.AlterColumn("ZipCode", "Address", "TEXT"); });
        }

        [Fact]
        public void AddPrimaryKey_to_TableRebuild()
        {
            SimpleRebuildTest(migrate => { migrate.AddPrimaryKey("PK_Address", "Address", new []{"Id"}); });
        }

        [Fact]
        public void DropColumn_to_TableRebuild()
        {
            SimpleRebuildTest(migrate=> migrate.DropColumn("HouseNumber","Address"));
        }

        [Fact]
        public void DropPrimaryKey_to_TableRebuild()
        {
            SimpleRebuildTest(migrate => { migrate.DropPrimaryKey("PK_Address", "Address"); });
        }

        [Fact]
        public void AddForeignKey_to_TableRebuild()
        {
            SimpleRebuildTest(migrate => { migrate.AddForeignKey("FK_Contact", "Address", new []{"Contact_Id"}, "Contacts"); });
        }

        [Fact]
        public void DropForeignKey_to_TableRebuild()
        {
            SimpleRebuildTest(migrate => { migrate.DropForeignKey("FK_Owner", "Address"); });
        }

        private void SimpleRebuildTest(Action<MigrationBuilder> migration)
        {
            var t = "Address";
            var operations = Transform(migration, model =>
                {
                    model.Entity("Contacts", b =>
                        {
                            b.Property<int>("Id");
                            b.Key("Id");
                        });
                    model.Entity(t, b =>
                        {
                            b.Property<string>("ZipCode").Required();
                            b.Property<int>("Contact_Id");
                            b.Property<int>("Id");
                            b.Reference("Contacts").InverseCollection().ForeignKey("Contact_Id");
                            b.Key("Id");
                        });
                });

            var steps = Assert.IsType<TableRebuildOperation>(operations[0]);

            var columns = new[] { "Id", "Contact_Id", "ZipCode" };
            Assert.Collection(steps.Operations,
                AssertRenameTemp(t),
                AssertCreateTable(t, columns),
                AssertMoveData(t, columns, columns),
                AssertDropTemp(t));
        }

        [Fact]
        public void RenameColumn_to_TableRebuild()
        {
            var operations = Transform(m => { m.RenameColumn("OldName", "A", "NewName"); }, model =>
                {
                    model.Entity("A", b =>
                        {
                            b.Property<string>("Id");
                            b.Property<string>("NewName");
                            b.Key("Id");
                        });
                });

            var steps = Assert.IsType<TableRebuildOperation>(operations[0]);
            Assert.Collection(steps.Operations,
                AssertRenameTemp("A"),
                AssertCreateTable("A", new[] { "Id", "NewName" }),
                AssertMoveData("A", new[] { "Id", "OldName" }, new[] { "Id", "NewName" }),
                AssertDropTemp("A"));
        }

        [Fact]
        public void Rebuild_filters_obviated_operations()
        {
            var t = "TableName";
            var operations = Transform(migrate =>
                {
                    migrate.DropColumn("Dropped", t);
                    migrate.AlterColumn("Altered", t, "TEXT", nullable: true);
                    migrate.AddColumn("New", t, "TEXT");
                    migrate.CreateIndex("IDX_A", t, new[] { "Indexed" }, unique: true);
                    migrate.AddPrimaryKey("PK_A", t, new[] { "Key" });
                }, model =>
                    {
                        model.Entity(t, b =>
                            {
                                b.Property<string>("Altered");
                                b.Property<string>("New");
                                b.Property<string>("Key");
                                b.Property<string>("Indexed");
                                b.Index("Indexed").Unique();
                                b.Key("Key");
                            });
                    });

            var steps = Assert.IsType<TableRebuildOperation>(operations[0]);

            Assert.Collection(steps.Operations,
                AssertRenameTemp(t),
                AssertCreateTable(t, new[] { "Key", "Altered", "Indexed", "New" }, new[] { "Key" }),
                AssertMoveData(t, new[] { "Key", "Altered", "Indexed" }, new[] { "Key", "Altered", "Indexed" }),
                AssertDropTemp(t),
                AssertCreateIndex(t, new[] { "Indexed" }, unique: true));
        }

        protected override SqliteOperationTransformer CreateTransformer()
            => new SqliteOperationTransformer(
                new ModelDiffer(
                    new SqliteTypeMapper(),
                    new SqliteMetadataExtensionProvider(),
                    new MigrationAnnotationProvider()));
    }
}
