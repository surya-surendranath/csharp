// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq.Expressions;
using JetBrains.Annotations;

namespace Microsoft.Data.Entity.Query.Compiler
{
    public interface IQueryCompiler
    {
        CompiledQuery CompileQuery<TResult>([NotNull] Expression query);
        CompiledQuery CompileAsyncQuery<TResult>([NotNull] Expression query);
    }
}
