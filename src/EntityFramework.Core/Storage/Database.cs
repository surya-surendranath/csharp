// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Data.Entity.ChangeTracking.Internal;
using Microsoft.Data.Entity.Internal;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Utilities;
using Microsoft.Framework.Logging;

namespace Microsoft.Data.Entity.Storage
{
    public abstract class Database : IDatabase
    {
        private readonly LazyRef<ILogger> _logger;

        protected Database(
            [NotNull] IModel model,
            [NotNull] ILoggerFactory loggerFactory)
        {
            Check.NotNull(model, nameof(model));
            Check.NotNull(loggerFactory, nameof(loggerFactory));

            Model = model;

            _logger = new LazyRef<ILogger>(loggerFactory.CreateLogger<Database>);
        }

        public virtual IModel Model { get; }

        public virtual ILogger Logger => _logger.Value;

        public abstract int SaveChanges(IReadOnlyList<InternalEntityEntry> entries);

        public abstract Task<int> SaveChangesAsync(
            IReadOnlyList<InternalEntityEntry> entries,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}
