using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using Mono.Cecil;

namespace DeobfuscateMain
{
	class Deobfuscator
	{
		class PatcherAssembly
		{
			public string assemblyFileName;
			public string patcherClass;
			public PatcherAssembly(XmlNode node)
			{
				foreach (XmlNode attr in node.Attributes)
				{
					if (attr.Name.ToLower().Equals("file"))
					{
						this.assemblyFileName = attr.Value;
					}
					else if (attr.Name.ToLower().Equals("class"))
					{
						this.patcherClass = attr.Value;
					}
				}
			}
		}

		private static string GetContainingFolder(string path)
		{
			string ret = null;
			int lastSlash = path.LastIndexOf("\\");
			if (lastSlash == -1)
				lastSlash = path.LastIndexOf("/");
			if (lastSlash != -1)
			{
				ret = path.Substring(0, lastSlash);
			}
			return ret;
		}

		private static string ownFolder;
		public static Assembly LoadPublicAssembly(object sender, ResolveEventArgs args)
		{
			string path = ownFolder + Path.PathSeparator + args.Name;
			if (!File.Exists(path))
			{
				path = ownFolder + Path.PathSeparator + "patchers" + Path.PathSeparator + args.Name;
				if (!File.Exists(path))
					return null;
			}
			return Assembly.LoadFrom(path);
		}

		public static void Main(string[] args)
		{
			Console.WriteLine("Assembly-CSharp Deobfuscator for 7 Days to Die [by the 7 Days to Die Modding Community]");

			ownFolder = GetContainingFolder(Assembly.GetEntryAssembly().Location);
			if (ownFolder == null) {
				Console.WriteLine("ERROR : Unable to retrieve the folder containing deobfuscate!");
				System.Threading.Thread.Sleep(10000);
				return;
			}
			if (args.Length == 0 || !args[0].ToLower().EndsWith(".dll"))
			{
				Console.WriteLine("Usage : deobfuscate \"<path to Assembly-CSharp.dll>\"");
				Console.WriteLine("Alternatively, you can drag and drop Assembly-CSharp.dll into deobfuscate.");
				System.Threading.Thread.Sleep(10000);
				return;
			}
			string managedFolder = GetContainingFolder(args[0]);
			if (managedFolder == null) {
				Console.WriteLine("ERROR : Unable to retrieve the folder containing Assembly-CSharp.dll!");
				System.Threading.Thread.Sleep(10000);
				return;
			}

			Logger mainLogger = new Logger(ownFolder + Path.DirectorySeparatorChar + "mainlog.txt", null, true);
			mainLogger.Log("Started logging to mainlog.txt.");

			string patchersPath = ownFolder + Path.DirectorySeparatorChar + "patchers";
			if (!Directory.Exists(patchersPath)) {
				Directory.CreateDirectory(patchersPath);
			}
			if (!File.Exists (patchersPath + Path.DirectorySeparatorChar + "patchers.xml")) {
				mainLogger.Log("");
				mainLogger.Log("There are no patches to apply (patchers.xml doesn't exist)! Exiting.");
				mainLogger.Close();
				System.Threading.Thread.Sleep(10000);
				return;
			}

			XmlDocument patchersDoc = new XmlDocument();
			try {
				patchersDoc.Load(patchersPath + Path.DirectorySeparatorChar + "patchers.xml");
			} catch (Exception e) {
				mainLogger.Log("");
				mainLogger.Log("ERROR : Unable to load patchers.xml : ");
				mainLogger.Log (e.ToString ());
				mainLogger.Close();
				System.Threading.Thread.Sleep(10000);
				return;
			}

			List<PatcherAssembly> assemblies = new List<PatcherAssembly> ();
			XmlNode curNode = patchersDoc.DocumentElement.FirstChild;
			if (curNode != null) {
				do {
					assemblies.Add(new PatcherAssembly(curNode));
				} while ((curNode = curNode.NextSibling) != null);
			}
			if (assemblies.Count == 0) {
				mainLogger.Log("");
				mainLogger.Log("There are no patches to apply (none listed in patchers.xml)! Exiting.");
				mainLogger.Close();
				System.Threading.Thread.Sleep(10000);
				return;
			}

			DefaultAssemblyResolver resolver = new DefaultAssemblyResolver();
			resolver.AddSearchDirectory(managedFolder);

			AssemblyDefinition csharpDef;
			try {
				csharpDef = AssemblyDefinition.ReadAssembly(args[0], new ReaderParameters{ AssemblyResolver = resolver });
			} catch (Exception e) {
				mainLogger.Log("");
				mainLogger.Log("ERROR : Unable to load Assembly-CSharp.dll :");
				mainLogger.Log(e.ToString ());
				mainLogger.Close();
				System.Threading.Thread.Sleep(10000);
				return;
			}
			if (csharpDef.Modules.Count == 0)
			{
				mainLogger.Log("");
				mainLogger.Log("ERROR : Assembly-CSharp.dll is invalid!");
				mainLogger.Close();
				System.Threading.Thread.Sleep(10000);
				return;
			}
			if (csharpDef.Modules[0].GetType("Deobfuscated") != null)
			{
				mainLogger.Log("");
				mainLogger.Log("Assembly-CSharp already is deobfuscated!");
				mainLogger.Close();
				System.Threading.Thread.Sleep(10000);
				return;
			}

			mainLogger.Log("Deobfuscating Assembly-CSharp.dll...");
			AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler (LoadPublicAssembly);

			foreach (PatcherAssembly curPatcher in assemblies) {
				string patcherName = 
					(curPatcher.assemblyFileName.IndexOf(".") != 0) ? curPatcher.assemblyFileName.Substring(0,curPatcher.assemblyFileName.LastIndexOf(".")) : curPatcher.assemblyFileName;
				string[] authors = new string[]{ "the 7 Days to Die Modding Community" }; 


				Assembly patcherAssembly;
				try {
					patcherAssembly = Assembly.LoadFrom(patchersPath + Path.DirectorySeparatorChar + patcherName + ".dll");
				} catch (Exception e) {
					mainLogger.Log("ERROR : Unable to load the patcher " + patcherName + " :");
					mainLogger.Log(e.ToString());
					continue;
				}
				Type patcherType = patcherAssembly.GetType(curPatcher.patcherClass);
				MethodInfo getNameMethod = patcherType.GetMethod("getName", new Type[0]);
				if (getNameMethod != null)
					patcherName = (string)getNameMethod.Invoke(null, new object[0]);
				MethodInfo getAuthorsMethod = patcherType.GetMethod("getAuthors", new Type[0]);
				if (getAuthorsMethod != null)
					authors = (string[])getAuthorsMethod.Invoke(null, new object[0]);
				MethodInfo patchMethod = patcherType.GetMethod("Patch", new Type[]{typeof(Logger), typeof(AssemblyDefinition), typeof(AssemblyDefinition)});
				if (patchMethod == null) {
					mainLogger.Log("ERROR : Unable to find the " + curPatcher.patcherClass + ".Patch(Logger,AssemblyDefinition,AssemblyDefinition) method for the patcher " + curPatcher.assemblyFileName + "!");
					continue;
				}
				string authorsString = "";
				foreach (string curAuthor in authors) {
					if (authorsString.Length > 0)
						authorsString += ",";
					authorsString += curAuthor;
				}
				mainLogger.Log ("Executing the patcher \"" + patcherName + "\" (by " + authorsString + ")...");
				try {
					Logger curLogger = new Logger (ownFolder + Path.DirectorySeparatorChar + "log_" + patcherName + ".txt", null, false);
					patchMethod.Invoke(null, new object[]{ curLogger, csharpDef, null });
					curLogger.Close();
				} catch (TargetInvocationException e) {
					mainLogger.Log ("ERROR : Invoking the Patch method for " + patcherName + " resulted in an exception :");
					mainLogger.Log(e.InnerException.ToString());
				} catch (Exception e) {
					mainLogger.Log ("ERROR : An exception occured while trying to invoke the Patch method of " + patcherName + " :");
					mainLogger.Log(e.ToString());
				}
			}

			csharpDef.Modules[0].Types.Add(new TypeDefinition("", "Deobfuscated", 
				Mono.Cecil.TypeAttributes.AutoLayout | Mono.Cecil. TypeAttributes.Public | 
					Mono.Cecil.TypeAttributes.AnsiClass | Mono.Cecil.TypeAttributes.BeforeFieldInit));

			string outputPath = managedFolder + Path.DirectorySeparatorChar + "Assembly-CSharp.deobf.dll";
			mainLogger.Log("Saving the new assembly to " + outputPath + " ...");
			try
			{
				csharpDef.Write(outputPath);
			}
			catch (Exception e)
			{
				mainLogger.Log("");
				mainLogger.Log("Unable to save the assembly : ");
				mainLogger.Log(e.ToString());
				System.Threading.Thread.Sleep(10000);
				return;
			}

			mainLogger.Log("");
			mainLogger.Log("Success.");
			mainLogger.Close();
			System.Threading.Thread.Sleep(10000);
		}
	}
}
