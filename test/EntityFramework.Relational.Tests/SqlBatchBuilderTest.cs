// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Storage;
using Xunit;

namespace Microsoft.Data.Entity
{
    public class SqlBatchBuilderTest
    {
        [Fact]
        public void SqlBatchBuilder_correctly_groups_multiple_statements_into_one_batch()
        {
            var batchBuilder = new SqlBatchBuilder(new TestTypeMapper());
            batchBuilder.AppendLine("Statement1");
            batchBuilder.AppendLine("Statement2");
            batchBuilder.AppendLine("Statement3");
            batchBuilder.EndBatch();

            Assert.Equal(1, batchBuilder.RelationalCommands.Count);
            Assert.Equal(
                @"Statement1
Statement2
Statement3
", batchBuilder.RelationalCommands[0].CommandText);
        }

        [Fact]
        public void SqlBatchBuilder_correctly_produces_multiple_batches()
        {
            var batchBuilder = new SqlBatchBuilder(new TestTypeMapper());
            batchBuilder.AppendLine("Statement1");
            batchBuilder.EndBatch();
            batchBuilder.AppendLine("Statement2");
            batchBuilder.AppendLine("Statement3");
            batchBuilder.EndBatch();
            batchBuilder.AppendLine("Statement4");
            batchBuilder.AppendLine("Statement5");
            batchBuilder.AppendLine("Statement6");
            batchBuilder.EndBatch();

            Assert.Equal(3, batchBuilder.RelationalCommands.Count);

            Assert.Equal(
                @"Statement1
", batchBuilder.RelationalCommands[0].CommandText);

            Assert.Equal(
                @"Statement2
Statement3
", batchBuilder.RelationalCommands[1].CommandText);

            Assert.Equal(
                @"Statement4
Statement5
Statement6
", batchBuilder.RelationalCommands[2].CommandText);
        }

        [Fact]
        public void SqlBatchBuilder_ignores_empty_batches()
        {
            var batchBuilder = new SqlBatchBuilder(new TestTypeMapper());
            batchBuilder.AppendLine("Statement1");
            batchBuilder.EndBatch();
            batchBuilder.EndBatch();
            batchBuilder.EndBatch();
            batchBuilder.AppendLine("Statement2");
            batchBuilder.AppendLine("Statement3");
            batchBuilder.EndBatch();
            batchBuilder.EndBatch();

            Assert.Equal(2, batchBuilder.RelationalCommands.Count);

            Assert.Equal(
                @"Statement1
", batchBuilder.RelationalCommands[0].CommandText);

            Assert.Equal(
                @"Statement2
Statement3
", batchBuilder.RelationalCommands[1].CommandText);
        }

        private class TestTypeMapper : RelationalTypeMapper
        {
            protected override IReadOnlyDictionary<Type, RelationalTypeMapping> SimpleMappings
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            protected override IReadOnlyDictionary<string, RelationalTypeMapping> SimpleNameMappings
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            protected override string GetColumnType(IProperty property)
            {
                throw new NotImplementedException();
            }
        }
    }
}