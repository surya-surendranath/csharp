﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Entity.Query;
using Microsoft.Data.Entity.Query.Methods;
using Microsoft.Data.Entity.SqlServer.Query.Methods;

namespace Microsoft.Data.Entity.Sqlite
{
    public class SqliteCompositeMemberTranslator : RelationalCompositeMemberTranslator
    {
        private readonly List<IMemberTranslator> _sqliteTranslators = new List<IMemberTranslator>
        {
            new StringLengthTranslator()
        };

        protected override IReadOnlyList<IMemberTranslator> Translators
            => base.Translators.Concat(_sqliteTranslators).ToList();
    }
}
