using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Mono.Cecil;
using System.Reflection;
using DeobfuscateMain;

namespace ManualDeobfuscator
{
	class ManualDeobfuscator
	{
		public static string getName()
		{
			return "ManualDeobfuscator";
		}
		public static string[] getAuthors()
		{
			return new string[]{ "Alloc", "DerPopo" };
		}

		public static void Patch(Logger logger, AssemblyDefinition asmCSharp, AssemblyDefinition __reserved)
		{
			PatchHelpers.logger = logger;
			ManualPatches.applyManualPatches(asmCSharp.MainModule);
			ManualPatches.FinalizeNormalizing();

			logger.Log(Logger.Level.KEYINFO, String.Format("Successful: {0} / Failed: {1}", PatchHelpers.success, PatchHelpers.errors));
		}
	}
}
