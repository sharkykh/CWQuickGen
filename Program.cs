namespace CWQuickGen
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Xml;

    class Program
    {
        private static CW cwInterface;

        private static string sourcePath;
        private static string targetPath;

        static void Main(string[] args)
        {
            string cfgFile = Path.ChangeExtension(Assembly.GetEntryAssembly().Location, ".cfg");

            string cwPath = null;
            if (File.Exists(cfgFile))
                cwPath = File.ReadAllText(cfgFile).Trim();

            if (cwPath == null || !CW.VerifyPath(cwPath, true))
            {
                string newPath = null;
                while (newPath == null || !CW.VerifyPath(newPath, true))
                {
                    Console.WriteLine($"Provide the path to CodeWalker (needed for {CW.DllName}):");
                    newPath = Console.ReadLine();

                    if (newPath == "")
                        return;
                }

                if (newPath != cwPath)
                {
                    cwPath = newPath;
                    File.WriteAllText(cfgFile, cwPath);
                }
            }

            cwInterface = new CW(cwPath);

            if (args.Length != 3)
            {
                Console.WriteLine("1st argument: File type - rel OR ymt");
                Console.WriteLine("2nd argument: Target path - where *.rel (or *.ymt) files will get created");
                Console.WriteLine("3rd argument: Sources path - where *.rel.xml (or *.ymt.pso.xml) files are located");
                return;
            }

            string fileType = args[0];
            targetPath = Path.GetFullPath(args[1]);
            sourcePath = Path.GetFullPath(args[2]);

            if (!Directory.Exists(sourcePath))
            {
                Console.WriteLine($"Sources path does not exist: {sourcePath}");
                return;
            }

            if (!Directory.Exists(targetPath))
                Directory.CreateDirectory(targetPath);

            if (fileType == "rel")
            {
                XmlToRel();
                // RelToXml();
            }
            else if (fileType == "ymt")
            {
                XmlToYmt();
                // YmtToXml();
            }
            else
            {
                Console.WriteLine($"Unknown type {fileType}.");
                return;
            }

            Console.WriteLine("Done.");
            //Console.ReadLine();
        }

        private static void XmlToRel()
        {
            string[] xmlFiles = Directory.GetFiles(sourcePath, "*.rel.xml", SearchOption.AllDirectories);

            foreach (string xmlFile in xmlFiles)
            {
                string relFileName = Path.GetFileNameWithoutExtension(xmlFile);
                string relFile = Path.Combine(targetPath, relFileName);

                // RelFile
                dynamic rel = FromXmlRelFile(xmlFile);

                byte[] binaryData = rel.Save();
                File.WriteAllBytes(relFile, binaryData);
                Console.WriteLine($" V {relFileName}");
            }
        }

        private static void RelToXml()
        {
            string[] relFiles = Directory.GetFiles(sourcePath, "*.rel", SearchOption.AllDirectories);

            foreach (string relFile in relFiles)
            {
                string xmlFileName = Path.GetFileName(relFile) + ".xml";
                string xmlFile = Path.Combine(targetPath, xmlFileName);

                // RelFile
                dynamic rel = FromRelFile(relFile);

                Type RelXml = cwInterface.GetType("GameFiles.RelXml");
                MethodInfo getXml = RelXml.GetMethod("GetXml");
                string xmlData = (string)getXml.Invoke(null, new object[] { rel });

                File.WriteAllText(xmlFile, xmlData);
                Console.WriteLine($@" V {xmlFileName}");
            }
        }

        private static void XmlToYmt()
        {
            string[] xmlFiles = Directory.GetFiles(sourcePath, "*.ymt.pso.xml", SearchOption.AllDirectories);

            foreach (string xmlFile in xmlFiles)
            {
                // remove .pso.xml
                string ymtFileName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(xmlFile));
                string ymtFile = Path.Combine(targetPath, ymtFileName);

                // PsoFile
                dynamic pso = FromPsoXmlFile(xmlFile);

                byte[] binaryData = pso.Save();
                File.WriteAllBytes(ymtFile, binaryData);
                Console.WriteLine($" V {ymtFileName}");
            }
        }

        /// <returns>RelFile</returns>
        private static dynamic FromXmlRelFile(string fileName)
        {
            XmlDocument doc = new XmlDocument();
            string fileData = File.ReadAllText(fileName);
            if (!string.IsNullOrEmpty(fileData))
            {
                doc.LoadXml(fileData);
            }

            // RelFile rel = XmlRel.GetRel(fileData);
            Type XmlRel = cwInterface.GetType("GameFiles.XmlRel");
            MethodInfo getRel = XmlRel.GetMethod(
                "GetRel",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new Type[] { typeof(XmlDocument) },
                null
            );
            // RelFile
            return getRel.Invoke(null, new object[] { doc });
        }

        /// <returns>RelFile</returns>
        private static dynamic FromRelFile(string fileName)
        {
            byte[] fileData = File.ReadAllBytes(fileName);

            // var rel = new RelFile();
            Type RelFile = cwInterface.GetType("GameFiles.RelFile");
            ConstructorInfo ctor = RelFile.GetConstructor(Type.EmptyTypes);
            dynamic rel = ctor.Invoke(null);

            rel.Load(fileData, null);
            // RelFile
            return rel;
        }

        /// <returns>PsoFile</returns>
        private static dynamic FromPsoXmlFile(string fileName)
        {
            XmlDocument doc = new XmlDocument();
            string fileData = File.ReadAllText(fileName);
            if (!string.IsNullOrEmpty(fileData))
            {
                doc.LoadXml(fileData);
            }

            // var pso = XmlPso.GetPso(doc);
            Type XmlPso = cwInterface.GetType("GameFiles.XmlPso");
            MethodInfo getPso = XmlPso.GetMethod(
                "GetPso",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new Type[] { typeof(XmlDocument) },
                null
            );
            // PsoFile
            return getPso.Invoke(null, new object[] { doc });
        }
    }

    /// <summary>
    /// Dynamic interface for <c>CodeWalker.Core</c>
    /// </summary>
    class CW
    {
        public const string DllName = "CodeWalker.Core.dll";
        private Assembly cwDLL;

        /// <param name="cwPath">Path to CodeWalker's root directory.</param>
        public CW(string cwPath)
        {
            if (!VerifyPath(cwPath))
                return;

            // Depdenencies for CodeWalker.Core.dll
            // https://github.com/dexyfex/CodeWalker/blob/master/CodeWalker.Core/CodeWalker.Core.csproj
            string[] dependencies = new string[] {
                "SharpDX.dll",
                "SharpDX.Mathematics.dll"
            };
            InitializeAssembly(cwPath, dependencies);

            string cwDllPath = Path.Combine(cwPath, DllName);
            this.cwDLL = Assembly.LoadFile(cwDllPath);
        }

        /// <summary>
        /// Verify path is CodeWalker's root directory
        /// </summary>
        /// <param name="path">Path to test.</param>
        /// <param name="print">Log the error to the console?</param>
        /// <returns></returns>
        public static bool VerifyPath(string path, bool print = false)
        {
            if (path == "")
                return false;

            if (!Directory.Exists(path))
            {
                if (print) Console.WriteLine($"Invalid folder: {path}");
                return false;
            }

            string dllPath = Path.Combine(path, DllName);
            if (!File.Exists(dllPath))
            {
                if (print) Console.WriteLine($"DLL not found: {dllPath}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get a CodeWalker.* type.
        /// </summary>
        /// <param name="typeName">Type name in the CodeWalker namespace.</param>
        /// <returns>The requested type, or null if not found.</returns>
        public Type GetType(string typeName)
        {
            return cwDLL.GetType($"CodeWalker.{typeName}");
        }

        #region Dynamic loading of DLLs
        // Dynamic loading of DLLs
        // Based on https://stackoverflow.com/a/30214970

        /// <summary>
        /// Call this method at the beginning of the program
        /// </summary>
        /// <param name="absoluteFolder">The absolute path to the folder containing the DLL files.</param>
        /// <param name="allowed">A list containing the DLL files to load.</param>
        private static void InitializeAssembly(string absoluteFolder, string[] allowed)
        {
            AppDomain.CurrentDomain.AssemblyResolve += delegate (object sender, ResolveEventArgs args)
            {
                string assemblyFile = (args.Name.Contains(","))
                    ? args.Name.Substring(0, args.Name.IndexOf(','))
                    : args.Name;

                assemblyFile += ".dll";

                // Forbid non handled dll's
                if (!allowed.Contains(assemblyFile))
                {
                    return null;
                }

                // string absoluteFolder = new FileInfo((new System.Uri(Assembly.GetExecutingAssembly().CodeBase)).LocalPath).Directory.FullName;
                string targetPath = Path.Combine(absoluteFolder, assemblyFile);

                try
                {
                    return Assembly.LoadFile(targetPath);
                }
                catch (Exception)
                {
                    return null;
                }
            };
        }
        #endregion
    }
}
