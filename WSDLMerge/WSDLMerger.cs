using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.IO;
using System.Net;

namespace WSDLMerge
{
    internal static class WSDLMerger
    {
        public const string WSDLNamespace = "http://schemas.xmlsoap.org/wsdl/";

        public const string XSDNamespace = "http://www.w3.org/2001/XMLSchema";

        /// <summary>
        /// Merge WSDL and XSD in filename and write it to destination
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="destination"></param>
        public static void Merge ( string filename, string destination )
        {
            try
            {
                // load WSDL
                XmlDocument wsdl = new XmlDocument ();
                wsdl.Load ( filename );

                // add standard namespaces
                XmlNamespaceManager manager = PrepareNamespaceManager ( wsdl );

                // verify it is a WSDL file
                if ( !VerifyWSDL ( wsdl, manager ) )
                {
                    Console.WriteLine ( "Error: Does not seem to be a WSDL file!" );
                    return;
                }

                // we need the types element so either look it up or create it
                XmlElement typesElement = CreateOrFindTypes ( wsdl, manager );
                if ( typesElement == null )
                {
                    Console.WriteLine ( "Error: definitions/types cannot be found nor created!" );
                    return;
                }

                // process all imports recursively
                ProcessImports ( filename, wsdl, typesElement, manager );

                // write result
                Console.WriteLine ( "Saving merged WSDL" );
                wsdl.Save ( destination );
            }
            catch ( Exception ex )
            {
                Console.WriteLine ( ex.ToString () );
            }
        }

        /// <summary>
        /// Prepare namespace manager for usage (add standard namespaces)
        /// </summary>
        /// <param name="wsdl"></param>
        /// <returns></returns>
        private static XmlNamespaceManager PrepareNamespaceManager ( XmlDocument wsdl )
        {
            XmlNamespaceManager manager = new XmlNamespaceManager ( wsdl.NameTable );
            manager.AddNamespace ( "wsdl", WSDLNamespace );
            manager.AddNamespace ( "xsd", XSDNamespace );
            return manager;
        }

        /// <summary>
        /// Verify the file is a WSDL
        /// </summary>
        /// <param name="wsdl"></param>
        /// <param name="manager"></param>
        /// <returns></returns>
        private static bool VerifyWSDL ( XmlDocument wsdl, XmlNamespaceManager manager )
        {
            XmlNode node = wsdl.SelectSingleNode ( "/wsdl:definitions", manager );
            if ( node == null )
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        /// <summary>
        /// Find the types element from the WSDL file
        /// </summary>
        /// <param name="wsdl"></param>
        /// <param name="manager"></param>
        /// <returns></returns>
        private static XmlElement CreateOrFindTypes ( XmlDocument wsdl, XmlNamespaceManager manager )
        {
            XmlNode node = wsdl.SelectSingleNode ( "/wsdl:definitions/wsdl:types", manager );
            if ( node != null )
            {
                return node as XmlElement;
            }

            XmlElement types = wsdl.CreateElement ( "wsdl", "types", WSDLNamespace );

            XmlNode import = wsdl.SelectSingleNode ( "/wsdl:definitions/wsdl:import", manager );
            if ( import == null )
            {
                wsdl.DocumentElement.InsertBefore ( types, wsdl.DocumentElement.FirstChild );
            }
            else
            {
                wsdl.DocumentElement.InsertAfter ( types, import );
            }

            return types;
        }

        /// <summary>
        /// Process all types elements
        /// </summary>
        /// <param name="wsdl"></param>
        /// <param name="typesElement"></param>
        /// <param name="manager"></param>
        private static void ProcessImports ( string filename, XmlDocument wsdl, XmlElement typesElement, XmlNamespaceManager manager )
        {
            Dictionary<string, XmlElement> schemas = new Dictionary<string, XmlElement> ();

            XmlNodeList schemaNodes = typesElement.SelectNodes ( "xsd:schema", manager );
            List<XmlNode> removeList = new List<XmlNode>();
            foreach (XmlElement schemaElement in schemaNodes)
	        {
                if (ProcessSchema(filename, wsdl, schemaElement, manager, schemas, 0))
                {
                    removeList.Add(schemaElement);
                }
            }

            foreach (XmlNode node in removeList)
            {
                typesElement.RemoveChild(node);
            }

            foreach ( var schema in schemas.Values )
            {
                typesElement.AppendChild ( schema );
            }
        }

        private static bool ProcessSchema ( 
            string filename, 
            XmlDocument wsdl, 
            XmlElement rootElement, 
            XmlNamespaceManager manager, 
            Dictionary<string, XmlElement> schemas, 
            int level )
        {
            XmlNodeList imports;
            imports = rootElement.SelectNodes ( "xsd:import", manager );
            if (imports.Count == 0) return false;

            foreach ( XmlNode node in imports )
            {
                string importNamespace, importLocation;
                GetImportDetails ( node, filename, out importNamespace, out importLocation );

                string schemasKey = importNamespace + "{" + importLocation + "}";

                if ( schemas.ContainsKey ( schemasKey ) ) continue;

                if ( importLocation == null ) throw new InvalidOperationException ();

                Console.WriteLine ( "Importing namespace: {0}", importNamespace );
                Console.WriteLine ( "  from file: {0}", importLocation );

                XmlDocument schemaDocument = new XmlDocument ();
                schemaDocument.Load ( importLocation );

                XmlElement newSchema = wsdl.ImportNode ( schemaDocument.DocumentElement, true ) as XmlElement;
                
                ProcessSchemaInnerImports(manager, level, newSchema);

                ProcessSchemaIncludes(manager, filename, wsdl, newSchema);

                schemas.Add ( schemasKey, newSchema );

                ProcessSchema (
                    importLocation,
                    wsdl,
                    schemaDocument.DocumentElement,
                    PrepareNamespaceManager ( schemaDocument ),
                    schemas,
                    level + 1 );
            }

            return true;
        }

        /// <summary>
        /// Process include statements in the schema file
        /// </summary>
        /// <param name="manager"></param>
        /// <param name="wsdl"></param>
        /// <param name="filename"></param>
        /// <param name="newSchema"></param>
        private static void ProcessSchemaIncludes(XmlNamespaceManager manager, string filename, XmlDocument wsdl, XmlElement newSchema)
        {
            XmlNodeList includes = newSchema.SelectNodes("/xsd:include", manager);
            foreach (XmlNode includeNode in includes)
            {
                string importNamespace;
                string importLocation;

                GetImportDetails ( includeNode, filename, out importNamespace, out importLocation );

                Console.WriteLine("  + include file: {0}", importLocation);

                XmlDocument schemaDocument = new XmlDocument();
                schemaDocument.Load(importLocation);

                XmlElement includedSchema = wsdl.ImportNode(schemaDocument.DocumentElement, true) as XmlElement;
                XmlNode[] childNodes = new XmlNode[includedSchema.ChildNodes.Count];
                for (int i = 0; i < includedSchema.ChildNodes.Count; i++)
			    {
                        childNodes[i] = includedSchema.ChildNodes[i];
			    }
                foreach (XmlNode child in childNodes )
                {
                    includeNode.ParentNode.InsertAfter(child, includeNode);
                }
                includeNode.ParentNode.RemoveChild(includeNode);
            }
        }

        /// <summary>
        /// Process import statements in the schema files
        /// </summary>
        /// <param name="manager"></param>
        /// <param name="level"></param>
        /// <param name="newSchema"></param>
        private static void ProcessSchemaInnerImports(XmlNamespaceManager manager, int level, XmlElement newSchema)
        {
            XmlNodeList newImports = newSchema.SelectNodes("/xsd:import", manager);
            foreach (XmlNode importNode in newImports)
            {
                if (level == 0)
                {
                    newSchema.RemoveChild(importNode);
                }
                else
                {
                    if (importNode.Attributes["schemaLocation"] != null)
                    {
                        importNode.Attributes.RemoveNamedItem("schemaLocation");
                    }
                }
            }
        }

        /// <summary>
        /// Get information about an imported XSD file
        /// </summary>
        /// <param name="node"></param>
        /// <param name="filename"></param>
        /// <param name="importNamespace"></param>
        /// <param name="importLocation"></param>
        private static void GetImportDetails ( 
            XmlNode node, 
            string filename, 
            out string importNamespace, 
            out string importLocation )
        {
            if ( node is XmlElement )
            {
                XmlElement importElement = node as XmlElement;
                XmlAttribute a = importElement.Attributes["namespace"];
                if (a != null)
                {
                    importNamespace = a.Value;
                }
                else
                {
                    importNamespace = null;
                }

                if ( importElement.Attributes["schemaLocation"] != null )
                {
                    string location = importElement.Attributes["schemaLocation"].Value;

                    // check if this is a url
                    if (Uri.IsWellFormedUriString(location, UriKind.Absolute))
                    {
                        // location does not change
                        importLocation = location;
                    }
                    // otherwise try to open it as a file
                    else 
                    {
                        // create relative path
                        importLocation = Path.Combine(Path.GetDirectoryName(filename), location);
                        if (File.Exists(importLocation))
                        {
                            importLocation = ResolveRelativeFilePath(importLocation);
                        }
                        else
                        {
                            throw new InvalidOperationException("Could not load schema");
                        }
                    }
                }
                else
                {
                    importLocation = null;
                }
            }
            else
            {
                throw new InvalidOperationException ();
            }
        }

        /// <summary>
        /// Remove any .. from in between the path
        /// </summary>
        /// <param name="importLocation"></param>
        /// <returns></returns>
        private static string ResolveRelativeFilePath(string importLocation)
        {
            FileInfo f = new FileInfo(importLocation);
            importLocation = f.FullName;
            return importLocation;
        }


    }
}
