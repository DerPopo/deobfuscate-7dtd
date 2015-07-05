using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using Mono.Cecil;

namespace DeobfuscateMain
{
	public class Deobfuscator
	{
		private static AssemblyPath ownFolder;
		public static AssemblyPath sourceAssemblyPath;
		private static Logger mainLogger = null;

		class PatcherAssembly
		{
			public string assemblyFileName;
			public string patcherClass;

			public PatcherAssembly (XmlNode node)
			{
				foreach (XmlNode attr in node.Attributes)
				{
					if (attr.Name.ToLower ().Equals ("file"))
					{
						this.assemblyFileName = attr.Value;
					}
					else if (attr.Name.ToLower ().Equals ("class"))
					{
						this.patcherClass = attr.Value;
					}
				}
			}
		}

		public class AssemblyPath
		{
			public string path;
			public string filename;

			public AssemblyPath (string _path, string _filename)
			{
				path = _path;
				filename = _filename;
			}
		}

		private static AssemblyPath GetContainingFolder (string fullpath)
		{
			string path = null;
			string file = null;
			int lastSlash = fullpath.LastIndexOf ("\\");
			if (lastSlash == -1)
				lastSlash = fullpath.LastIndexOf ("/");
			if (lastSlash != -1) {
				path = fullpath.Substring (0, lastSlash);
				file = fullpath.Substring (lastSlash + 1);
			} else {
				if (ownFolder == null)
					return null;
				path = ownFolder.path;
				file = fullpath;
			}

			return new AssemblyPath (path, file);
		}

		public static Assembly LoadPublicAssembly (object sender, ResolveEventArgs args)
		{
			string path = ownFolder.path + Path.DirectorySeparatorChar + args.Name;
			if (!File.Exists (path))
			{
				path = ownFolder.path + Path.DirectorySeparatorChar + "patchers" + Path.DirectorySeparatorChar + args.Name;
				if (!File.Exists (path))
					return null;
			}
			return Assembly.LoadFrom (path);
		}

		private static void ErrorExit (string message, int returnCode = 1)
		{
			Console.WriteLine ();
			Logger.Level logLevel = (returnCode == 0) ? Logger.Level.KEYINFO : Logger.Level.ERROR;
			if (mainLogger != null)
			{
				if (message.Length > 0)
					mainLogger.Log(logLevel, message);
				mainLogger.Close ();
			}
			else
				Console.WriteLine(Logger.Level_ToString(logLevel) + message);

			Console.WriteLine ();
			Console.WriteLine ("Press any key to exit");
			Console.ReadKey ();
			Environment.Exit (returnCode);
		}

		public static void Main (string[] args)
		{
			args = new string[] { "E:\\Programme\\SteamLibrary\\SteamApps\\common\\7 Days To Die\\7DaysToDie_Data\\Managed\\Assembly-CSharp.o.dll" };
			Console.WriteLine ("Assembly-CSharp Deobfuscator for 7 Days to Die [by the 7 Days to Die Modding Community]");

			ownFolder = GetContainingFolder (Assembly.GetEntryAssembly ().Location);
			if (ownFolder == null) {
				ErrorExit("Unable to retrieve the folder containing Deobfuscator!");
			}
			bool verbosity = false;//(args.Length > 1) ? (args[0].ToLower().Equals("-v")) : false;
			if (File.Exists (ownFolder.path + Path.DirectorySeparatorChar + "config.xml"))
			{
				XmlDocument configDoc = new XmlDocument ();
				try {
					configDoc.Load(ownFolder.path + Path.DirectorySeparatorChar + "config.xml");
				} catch (Exception e) {
					Console.WriteLine(Logger.Level_ToString(Logger.Level.WARNING) + "Unable to load config.xml : " + e.ToString ());
				}
				XmlNodeList configElems = configDoc.DocumentElement.ChildNodes;
				foreach (XmlNode curElem in configElems) {
					if (!curElem.Name.ToLower().Equals("verbosity"))
						continue;
					XmlNode verbosityElem = curElem;
					XmlAttributeCollection verbosityAttrs = verbosityElem.Attributes;
					foreach (XmlNode curAttr in verbosityAttrs) {
						if (curAttr.Name.ToLower().Equals("enabled")) {
							verbosity = curAttr.Value.ToLower().Equals("true");
							break;
						}
					}
				}
			}
			else
				Console.WriteLine(ownFolder.path + Path.DirectorySeparatorChar + "config.xml");
			mainLogger = new Logger (ownFolder.path + Path.DirectorySeparatorChar + "mainlog.txt", null, (int)(verbosity ? Logger.Level.INFO : Logger.Level.KEYINFO));
			mainLogger.Info("Started logging to mainlog.txt.");

			if ( args.Length == 0 || !args[0].ToLower().EndsWith(".dll") )
			{
				mainLogger.Write("Usage : deobfuscate \"<path to Assembly-CSharp.dll>\"");
				mainLogger.Write("Alternatively, you can drag and drop Assembly-CSharp.dll into deobfuscate.");
				ErrorExit("", 2);
			}
			AssemblyPath acsharpSource = GetContainingFolder (args [0]);
			if (!File.Exists (acsharpSource.path + Path.DirectorySeparatorChar + acsharpSource.filename)) {
				ErrorExit("Unable to retrieve the folder containing Assembly-CSharp.dll!");
			}
			sourceAssemblyPath = acsharpSource;

			string patchersPath = ownFolder.path + Path.DirectorySeparatorChar + "patchers";
			if (!Directory.Exists (patchersPath)) {
				Directory.CreateDirectory (patchersPath);
			}
			if (!File.Exists (patchersPath + Path.DirectorySeparatorChar + "patchers.xml")) {
				ErrorExit("There are no patches to apply (patchers.xml doesn't exist)! Exiting.", 3);
			}

			XmlDocument patchersDoc = new XmlDocument ();
			try {
				patchersDoc.Load (patchersPath + Path.DirectorySeparatorChar + "patchers.xml");
			} catch (Exception e) {
				ErrorExit("Unable to load patchers.xml : " + e.ToString ());
			}

			List<PatcherAssembly> assemblies = new List<PatcherAssembly> ();
			XmlNode curNode = patchersDoc.DocumentElement.FirstChild;
			if (curNode != null) {
				do {
					assemblies.Add (new PatcherAssembly (curNode));
				} while ((curNode = curNode.NextSibling) != null);
			}
			if (assemblies.Count == 0) {
				ErrorExit("There are no patches to apply (none listed in patchers.xml)! Exiting.", 3);
			}

			DefaultAssemblyResolver resolver = new DefaultAssemblyResolver ();
			resolver.AddSearchDirectory (acsharpSource.path);

			AssemblyDefinition csharpDef = null;
			AssemblyDefinition mscorlibDef = null;
			try {
				csharpDef = AssemblyDefinition.ReadAssembly (args [0], new ReaderParameters{ AssemblyResolver = resolver });
			} catch (Exception e) {
				ErrorExit("Unable to load Assembly-CSharp.dll :" + e.ToString ());
			}
			try {
				mscorlibDef = AssemblyDefinition.ReadAssembly(acsharpSource.path + Path.DirectorySeparatorChar + "mscorlib.dll", new ReaderParameters{ AssemblyResolver = resolver });
			} catch (Exception e) {
				mainLogger.Warning("Unable to load mscorlib.dll :" + e.ToString());
			}
			int csharpFileLen = (int)new FileInfo(args[0]).Length;

			if (csharpDef.Modules.Count == 0)
			{
				ErrorExit("Assembly-CSharp.dll is invalid!");
			}
			ModuleDefinition csharpModule = csharpDef.Modules[0];
			if (csharpModule.GetType("Deobfuscated") != null)
			{
				ErrorExit("Assembly-CSharp already is deobfuscated!");
			}

			mainLogger.KeyInfo("Deobfuscating Assembly-CSharp.dll...");
			AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler (LoadPublicAssembly);
			mainLogger.Write("___");
			foreach (PatcherAssembly curPatcher in assemblies) {
				mainLogger.Write();
				mainLogger.Write();

				string patcherName = 
					(curPatcher.assemblyFileName.IndexOf (".") != 0) ? curPatcher.assemblyFileName.Substring(0,curPatcher.assemblyFileName.LastIndexOf(".")) : curPatcher.assemblyFileName;
				string[] authors = new string[]{ "the 7 Days to Die Modding Community" }; 


				Assembly patcherAssembly;
				try {
					patcherAssembly = Assembly.LoadFrom (patchersPath + Path.DirectorySeparatorChar + patcherName + ".dll");
				} catch (Exception e) {
					mainLogger.Error("Unable to load the patcher " + patcherName + " :");
					mainLogger.Error(e.ToString ());
					continue;
				}
				Type patcherType = patcherAssembly.GetType (curPatcher.patcherClass);
				MethodInfo getNameMethod = patcherType.GetMethod ("getName", new Type[0]);
				if (getNameMethod != null)
					patcherName = (string)getNameMethod.Invoke (null, new object[0]);
				MethodInfo getAuthorsMethod = patcherType.GetMethod ("getAuthors", new Type[0]);
				if (getAuthorsMethod != null)
					authors = (string[])getAuthorsMethod.Invoke (null, new object[0]);
				MethodInfo patchMethod = patcherType.GetMethod ("Patch", new Type[]{typeof(Logger), typeof(AssemblyDefinition), typeof(AssemblyDefinition)});
				if (patchMethod == null) {
					mainLogger.Error("Unable to find the " + curPatcher.patcherClass + ".Patch(Logger,AssemblyDefinition,AssemblyDefinition) method for the patcher " + curPatcher.assemblyFileName + "!");
					continue;
				}
				string authorsString = "";
				foreach (string curAuthor in authors) {
					if (authorsString.Length > 0)
						authorsString += ",";
					authorsString += curAuthor;
				}
				mainLogger.KeyInfo("Executing patcher \"" + patcherName + "\" (by " + authorsString + ")...");
				try {
					Logger curLogger = new Logger (ownFolder.path + Path.DirectorySeparatorChar + "log_" + patcherName + ".txt", null, (int)(verbosity ? Logger.Level.INFO : Logger.Level.KEYINFO));
					patchMethod.Invoke (null, new object[]{ curLogger, csharpDef, null });
					curLogger.Close ();
				} catch (TargetInvocationException e) {
					mainLogger.Error ("ERROR : Invoking the Patch method for " + patcherName + " resulted in an exception :");
					mainLogger.Error (e.InnerException.ToString());
				} catch (Exception e) {
					mainLogger.Error ("ERROR : An exception occured while trying to invoke the Patch method of " + patcherName + " :");
					mainLogger.Error (e.ToString ());
				}
				mainLogger.Info("Writing the current Assembly-CSharp.dll to a MemoryStream...");
				MemoryStream asmCSharpStream = new MemoryStream(csharpFileLen + 2048 + 1024 * assemblies.Count);
				csharpDef.Write(asmCSharpStream);
				mainLogger.Info("Reading the current Assembly-CSharp.dll from the MemoryStream...");
				asmCSharpStream.Seek(0, SeekOrigin.Begin);
				csharpDef = AssemblyDefinition.ReadAssembly(asmCSharpStream, new ReaderParameters{ AssemblyResolver = resolver });
				asmCSharpStream.Close();
				csharpModule = csharpDef.Modules[0];
			}
			mainLogger.Write(); mainLogger.Write("___");

			if ((mscorlibDef != null) && (mscorlibDef.Modules.Count > 0))
			{
				csharpModule.Types.Add(new TypeDefinition("", "Deobfuscated", 
					Mono.Cecil.TypeAttributes.AutoLayout | Mono.Cecil. TypeAttributes.Public | 
					Mono.Cecil.TypeAttributes.AnsiClass | Mono.Cecil.TypeAttributes.BeforeFieldInit, csharpModule.Import(mscorlibDef.Modules[0].GetType("System.Object"))));
			}
			else
				mainLogger.Error("Unable to create the Deobufscated class!");

			string outputPath = acsharpSource.path + Path.DirectorySeparatorChar + "Assembly-CSharp.deobf.dll";
			mainLogger.KeyInfo ("Saving the new assembly to " + outputPath + " ...");
			try
			{
				csharpDef.Write (outputPath);
			}
			catch (Exception e)
			{
				ErrorExit ("Unable to save the assembly : " + e.ToString ());
			}

			ErrorExit ("Success.", 0);
		}
	}
}
