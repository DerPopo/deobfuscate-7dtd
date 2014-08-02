using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Mono.Cecil;
using System.Reflection;

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

		public static void Patch(DeobfuscateMain.Logger logger, AssemblyDefinition asmCSharp, AssemblyDefinition __reserved)
		{
			PatchHelpers.logger = logger;
			ManualPatches.applyManualPatches(asmCSharp.MainModule);
			ManualPatches.FinalizeNormalizing();

			logger.Log(String.Format("Successful: {0} / Failed: {1}", PatchHelpers.success, PatchHelpers.errors));
		}
	}
}
