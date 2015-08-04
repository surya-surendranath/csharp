// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using JetBrains.Annotations;

namespace Microsoft.Data.Entity.Query.Compiler
{
    public interface ICompiledQueryCache
    {
        CompiledQuery GetOrAdd(
            [NotNull] string cacheKey,
            [NotNull] Func<CompiledQuery> compiler);
    }
}
