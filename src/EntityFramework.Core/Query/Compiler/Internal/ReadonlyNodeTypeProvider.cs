// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using Microsoft.Data.Entity.Query.ResultOperators;
using Remotion.Linq.Parsing.Structure;
using Remotion.Linq.Parsing.Structure.NodeTypeProviders;

namespace Microsoft.Data.Entity.Query.Compiler.Internal
{
    public class ReadonlyNodeTypeProvider : INodeTypeProvider
    {
        private readonly INodeTypeProvider _nodeTypeProvider;

        private ReadonlyNodeTypeProvider(INodeTypeProvider nodeTypeProvider)
        {
            _nodeTypeProvider = nodeTypeProvider;
        }

        public bool IsRegistered(MethodInfo method) => _nodeTypeProvider.IsRegistered(method);

        public Type GetNodeType(MethodInfo method) => _nodeTypeProvider.GetNodeType(method);

        public static ReadonlyNodeTypeProvider CreateNodeTypeProvider()
        {
            var methodInfoBasedNodeTypeRegistry = MethodInfoBasedNodeTypeRegistry.CreateFromRelinqAssembly();

            methodInfoBasedNodeTypeRegistry
                .Register(QueryAnnotationExpressionNode.SupportedMethods, typeof(QueryAnnotationExpressionNode));

            methodInfoBasedNodeTypeRegistry
                .Register(IncludeExpressionNode.SupportedMethods, typeof(IncludeExpressionNode));

            methodInfoBasedNodeTypeRegistry
                .Register(ThenIncludeExpressionNode.SupportedMethods, typeof(ThenIncludeExpressionNode));

            var innerProviders
                = new INodeTypeProvider[]
                {
                    methodInfoBasedNodeTypeRegistry,
                    MethodNameBasedNodeTypeRegistry.CreateFromRelinqAssembly()
                };

            return new ReadonlyNodeTypeProvider(new CompoundNodeTypeProvider(innerProviders));
        }
    }
}
