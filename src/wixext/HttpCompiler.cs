// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolset.Http
{
    using System;
    using System.Collections.Generic;
    using System.Xml.Linq;
    using WixToolset.Data;
    using WixToolset.Extensibility;
    using WixToolset.Http.Tuples;

    /// <summary>
    /// The compiler for the WiX Toolset Http Extension.
    /// </summary>
    public sealed class HttpCompiler : BaseCompilerExtension
    {
        public override XNamespace Namespace => "http://wixtoolset.org/schemas/v4/wxs/http";

        /// <summary>
        /// Processes an element for the Compiler.
        /// </summary>
        /// <param name="sourceLineNumbers">Source line number for the parent element.</param>
        /// <param name="parentElement">Parent element of element to process.</param>
        /// <param name="element">Element to process.</param>
        /// <param name="contextValues">Extra information about the context in which this element is being parsed.</param>
        public override void ParseElement(Intermediate intermediate, IntermediateSection section, XElement parentElement, XElement element, IDictionary<string, string> context)
        {
            switch (parentElement.Name.LocalName)
            {
                case "ServiceInstall":
                    string serviceInstallName = context["ServiceInstallName"];
                    string serviceUser = String.IsNullOrEmpty(serviceInstallName) ? null : String.Concat("NT SERVICE\\", serviceInstallName);
                    string serviceComponentId = context["ServiceInstallComponentId"];

                    switch (element.Name.LocalName)
                    {
                        case "UrlReservation":
                            this.ParseUrlReservationElement(intermediate, section, element, serviceComponentId, serviceUser);
                            break;
                        default:
                            this.ParseHelper.UnexpectedElement(parentElement, element);
                            break;
                    }
                    break;
                case "Component":
                    string componentId = context["ComponentId"];

                    switch (element.Name.LocalName)
                    {
                        case "UrlReservation":
                            this.ParseUrlReservationElement(intermediate, section, element, componentId, null);
                            break;
                        default:
                            this.ParseHelper.UnexpectedElement(parentElement, element);
                            break;
                    }
                    break;
                default:
                    this.ParseHelper.UnexpectedElement(parentElement, element);
                    break;
            }
        }

        /// <summary>
        /// Parses a UrlReservation element.
        /// </summary>
        /// <param name="node">The element to parse.</param>
        /// <param name="componentId">Identifier of the component that owns this URL reservation.</param>
        /// <param name="securityPrincipal">The security principal of the parent element (null if nested under Component).</param>
        private void ParseUrlReservationElement(Intermediate intermediate, IntermediateSection section, XElement node, string componentId, string securityPrincipal)
        {
            SourceLineNumber sourceLineNumbers = this.ParseHelper.GetSourceLineNumbers(node);
            Identifier id = null;
            int handleExisting = HttpConstants.heReplace;
            string handleExistingValue = null;
            string sddl = null;
            string url = null;
            bool foundACE = false;

            foreach (XAttribute attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || this.Namespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                        case "Id":
                            id = this.ParseHelper.GetAttributeIdentifier(sourceLineNumbers, attrib);
                            break;
                        case "HandleExisting":
                            handleExistingValue = this.ParseHelper.GetAttributeValue(sourceLineNumbers, attrib);
                            switch (handleExistingValue)
                            {
                                case "replace":
                                    handleExisting = HttpConstants.heReplace;
                                    break;
                                case "ignore":
                                    handleExisting = HttpConstants.heIgnore;
                                    break;
                                case "fail":
                                    handleExisting = HttpConstants.heFail;
                                    break;
                                default:
                                    this.Messaging.Write(ErrorMessages.IllegalAttributeValue(sourceLineNumbers, node.Name.LocalName, "HandleExisting", handleExistingValue, "replace", "ignore", "fail"));
                                    break;
                            }
                            break;
                        case "Sddl":
                            sddl = this.ParseHelper.GetAttributeValue(sourceLineNumbers, attrib);
                            break;
                        case "Url":
                            url = this.ParseHelper.GetAttributeValue(sourceLineNumbers, attrib);
                            break;
                        default:
                            this.ParseHelper.UnexpectedAttribute(node, attrib);
                            break;
                    }
                }
                else
                {
                    this.ParseHelper.ParseExtensionAttribute(this.Context.Extensions, intermediate, section, node, attrib);
                }
            }

            // Need the element ID for child element processing, so generate now if not authored.
            if (null == id)
            {
                id = this.ParseHelper.CreateIdentifier("url", componentId, securityPrincipal, url);
            }

            // Parse UrlAce children.
            foreach (XElement child in node.Elements())
            {
                if (this.Namespace == child.Name.Namespace)
                {
                    switch (child.Name.LocalName)
                    {
                        case "UrlAce":
                            if (null != sddl)
                            {
                                this.Messaging.Write(ErrorMessages.IllegalParentAttributeWhenNested(sourceLineNumbers, "UrlReservation", "Sddl", "UrlAce"));
                            }
                            else
                            {
                                foundACE = true;
                                this.ParseUrlAceElement(intermediate, section, child, id.Id, securityPrincipal);
                            }
                            break;
                        default:
                            this.ParseHelper.UnexpectedElement(node, child);
                            break;
                    }
                }
                else
                {
                    this.ParseHelper.ParseExtensionElement(this.Context.Extensions, intermediate, section, node, child);
                }
            }

            // Url is required.
            if (null == url)
            {
                this.Messaging.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "Url"));
            }

            // Security is required.
            if (null == sddl && !foundACE)
            {
                this.Messaging.Write(HttpErrors.NoSecuritySpecified(sourceLineNumbers));
            }

            if (!this.Messaging.EncounteredError)
            {
                var row = (WixHttpUrlReservationTuple)this.ParseHelper.CreateRow(section, sourceLineNumbers, "WixHttpUrlReservation", id);
                row.HandleExisting = handleExisting;
                row.Sddl = sddl;
                row.Url = url;
                row.Component_ = componentId;

                if (this.Context.Platform == Platform.ARM)
                {
                    // Ensure ARM version of the CA is referenced.
                    this.ParseHelper.CreateSimpleReference(section, sourceLineNumbers, "CustomAction", "WixSchedHttpUrlReservationsInstall_ARM");
                    this.ParseHelper.CreateSimpleReference(section, sourceLineNumbers, "CustomAction", "WixSchedHttpUrlReservationsUninstall_ARM");
                }
                else
                {
                    // All other supported platforms use x86.
                    this.ParseHelper.CreateSimpleReference(section, sourceLineNumbers, "CustomAction", "WixSchedHttpUrlReservationsInstall");
                    this.ParseHelper.CreateSimpleReference(section, sourceLineNumbers, "CustomAction", "WixSchedHttpUrlReservationsUninstall");
                }
            }
        }

        /// <summary>
        /// Parses a UrlAce element.
        /// </summary>
        /// <param name="node">The element to parse.</param>
        /// <param name="urlReservationId">The URL reservation ID.</param>
        /// <param name="defaultSecurityPrincipal">The default security principal.</param>
        private void ParseUrlAceElement(Intermediate intermediate, IntermediateSection section, XElement node, string urlReservationId, string defaultSecurityPrincipal)
        {
            SourceLineNumber sourceLineNumbers = this.ParseHelper.GetSourceLineNumbers(node);
            Identifier id = null;
            string securityPrincipal = defaultSecurityPrincipal;
            int rights = HttpConstants.GENERIC_ALL;
            string rightsValue = null;
            
            foreach (XAttribute attrib in node.Attributes())
            {
                if (String.IsNullOrEmpty(attrib.Name.NamespaceName) || this.Namespace == attrib.Name.Namespace)
                {
                    switch (attrib.Name.LocalName)
                    {
                        case "Id":
                            id = this.ParseHelper.GetAttributeIdentifier(sourceLineNumbers, attrib);
                            break;
                        case "SecurityPrincipal":
                            securityPrincipal = this.ParseHelper.GetAttributeValue(sourceLineNumbers, attrib);
                            break;
                        case "Rights":
                            rightsValue = this.ParseHelper.GetAttributeValue(sourceLineNumbers, attrib);
                            switch (rightsValue)
                            {
                                case "all":
                                    rights = HttpConstants.GENERIC_ALL;
                                    break;
                                case "delegate":
                                    rights = HttpConstants.GENERIC_WRITE;
                                    break;
                                case "register":
                                    rights = HttpConstants.GENERIC_EXECUTE;
                                    break;
                                default:
                                    this.Messaging.Write(ErrorMessages.IllegalAttributeValue(sourceLineNumbers, node.Name.LocalName, "Rights", rightsValue, "all", "delegate", "register"));
                                    break;
                            }
                            break;
                        default:
                            this.ParseHelper.UnexpectedAttribute(node, attrib);
                            break;
                    }
                }
                else
                {
                    this.ParseHelper.ParseExtensionAttribute(this.Context.Extensions, intermediate, section, node, attrib);
                }
            }

            // Generate Id now if not authored.
            if (null == id)
            {
                id = this.ParseHelper.CreateIdentifier("ace", urlReservationId, securityPrincipal, rightsValue);
            }

            this.ParseHelper.ParseForExtensionElements(this.Context.Extensions, intermediate, section, node);

            // SecurityPrincipal is required.
            if (null == securityPrincipal)
            {
                this.Messaging.Write(ErrorMessages.ExpectedAttribute(sourceLineNumbers, node.Name.LocalName, "SecurityPrincipal"));
            }

            if (!this.Messaging.EncounteredError)
            {
                var row = (WixHttpUrlAceTuple)this.ParseHelper.CreateRow(section, sourceLineNumbers, "WixHttpUrlAce", id);
                row.WixHttpUrlReservation_ = urlReservationId;
                row.SecurityPrincipal = securityPrincipal;
                row.Rights = rights;
            }
        }
    }
}