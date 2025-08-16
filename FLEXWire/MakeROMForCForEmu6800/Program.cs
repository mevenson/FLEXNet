using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;

namespace MakeROMForCForEmu6800
{
    class Program
    {
        static string fileToConvert = "";
        static string outputFilename = "";

        static void usage()
        {
            Console.WriteLine("usage: MakeROMForCForEmu6800 -r=<rom stx filename> [-c=<output filename>]");
        }

        static void Main(string[] args)
        {
            foreach (string arg in args)
            {
                if (arg.Substring(0, 3).CompareTo("-r=") == 0)
                {
                    fileToConvert = arg.Replace("-r=", "");
                }
                if (arg.Substring(0, 3).CompareTo("-c=") == 0)
                {
                    outputFilename = arg.Replace("-c=", "");
                }
            }

            if (fileToConvert.Length > 0)
            {
                string directory = Path.GetDirectoryName(fileToConvert);
                string filename = Path.GetFileNameWithoutExtension(fileToConvert);

                if (outputFilename.Length == 0)
                    outputFilename = $"{Path.Combine(directory, filename)}.c";

                try
                {
                    using (BinaryReader reader = new BinaryReader(File.Open(fileToConvert, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                    {
                        try
                        {
                            List<string> outputLines = new List<string>();

                            int outputCount = 0;
                            StringBuilder sb = new StringBuilder();

                            while (reader.BaseStream.Position < reader.BaseStream.Length)
                            {
                                byte c = reader.ReadByte();
                                sb.Append($" 0x{c.ToString("X2")}");
                                outputCount++;

                                // we need to output the comma before we do <new line>, so chaeck if this is the last byte to outtput
                                if (reader.BaseStream.Position != reader.BaseStream.Length)
                                {
                                    sb.Append(",");
                                }

                                if (outputCount % 32 == 0)
                                {
                                    outputLines.Add(sb.ToString());
                                    sb.Clear();
                                }
                            }
                            if (sb.Length > 0)
                            {
                                outputLines.Add(sb.ToString());
                            }

                            using (TextWriter writer = new StreamWriter(File.Open(outputFilename, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)))
                            {
                                // now that we have all of the lines built - output to writer

                                writer.WriteLine("#include <stdint.h>");
                                writer.WriteLine("");

                                writer.WriteLine($"const uint8_t {filename.ToLower()}[{reader.BaseStream.Length}] =");
                                writer.WriteLine("{");
                                foreach(string s in outputLines)
                                {
                                    writer.WriteLine($"   {s}");
                                }
                                writer.WriteLine("};");
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
            else
                usage();
        }
    }
}
