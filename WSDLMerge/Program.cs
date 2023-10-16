using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml;

namespace WSDLMerge
{
    class Program
    {
        static void Main ( string[] args )
        {
            Console.WriteLine("WSDLMerge 1.3");
            Console.WriteLine("Copyright (c) 2011-2013 by Lenard Gunda");
            Console.WriteLine("Cloned and updated to .net 6 by Olgerik Albers");
            Console.WriteLine("");

            if ( args.Length < 1 )
            {
                Usage ();
                return;
            }
            
            string sourceFilename = args[0];
            string destinationFilename = null;
            if (args.Length > 1)
            {
                destinationFilename = args[1];
            }

            if (Uri.IsWellFormedUriString(sourceFilename, UriKind.Absolute))
            {
                if (args.Length < 2)
                {
                    Usage();
                    return;
                }

                destinationFilename = args[1];
            }
            else
            {
                if (File.Exists(sourceFilename) == false)
                {
                    Console.WriteLine("Error: .wsdl file does not exist!");
                    return;
                }

                FileInfo f = new FileInfo(sourceFilename);
                sourceFilename = f.FullName;
                
                if (destinationFilename == null)
                {
                    destinationFilename =
                        Path.Combine(
                            Path.GetDirectoryName(sourceFilename),
                            Path.GetFileNameWithoutExtension(sourceFilename) + "_merged" + Path.GetExtension(sourceFilename));
                }
            }

            Console.WriteLine ( "Processing: {0}", sourceFilename );
            Console.WriteLine ( "Will create: {0}", destinationFilename );

            WSDLMerger.Merge ( sourceFilename, destinationFilename );
        }

        private static void Usage ()
        {
            Console.WriteLine("Usage: WSDLMerge wsdlfile [outputfile]");
            Console.WriteLine("");
            Console.WriteLine("Arguments:");
            Console.WriteLine("  wsdlfile      path to wsdl file. Can be URL");
            Console.WriteLine("  [outputfile]  outputfile. required if input is URL");
            Console.WriteLine("");
            Console.WriteLine("Description:");
            Console.WriteLine("  Loads given WSDL file. Recursively scans for schema and/or wsdl references,");
            Console.WriteLine("  and merges those references into the original WSDL document. The result is");
            Console.WriteLine("  written to disk.");
            Console.WriteLine("");
        }
    }
}
