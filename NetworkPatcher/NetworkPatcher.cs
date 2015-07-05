using System;
using DeobfuscateMain;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System.Collections.Generic;

namespace NetworkPatcher
{
	public class NetworkPatcher
	{
		public static string getName()
		{
			return "PacketOrNotRelatedStuffPatcher";
		}
		public static string[] getAuthors()
		{
			return new string[]{ "DerPopo", "Alloc", "KaXaK" };
		}

		static MethodDefinition cctorMDef = null;
		static TypeDefinition packageTypeEnumDef = null;
		public static int success = 0;
		public static int error = 0;

		public static void Patch(Logger logger, AssemblyDefinition asmCSharp, AssemblyDefinition __reserved)
		{
			PatchMisc.Patch(logger, asmCSharp);
			//logger.Log(Logger.Level.KEYINFO, String.Format("Successful: {0} / Failed: {1}", success, error));
		}
	}
}

