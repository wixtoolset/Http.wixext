// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolset.Http
{
    using WixToolset.Data;
    using WixToolset.Extensibility;

    /// <summary>
    /// The WiX Toolset Http Extension.
    /// </summary>
    public sealed class HttpExtensionData : BaseExtensionData
    {
        /// <summary>
        /// Gets the default culture.
        /// </summary>
        /// <value>The default culture.</value>
        public override string DefaultCulture => "en-US";

        public override bool TryGetSymbolDefinitionByName(string name, out IntermediateSymbolDefinition symbolDefinition)
        {
            symbolDefinition = HttpSymbolDefinitions.ByName(name);
            return symbolDefinition != null;
        }

        public override Intermediate GetLibrary(ISymbolDefinitionCreator symbolDefinitions)
        {
            return Intermediate.Load(typeof(HttpExtensionData).Assembly, "WixToolset.Http.http.wixlib", symbolDefinitions);
        }
    }
}
