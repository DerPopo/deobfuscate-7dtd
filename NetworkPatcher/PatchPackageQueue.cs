using System;
using DeobfuscateMain;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Collections.Generic;

namespace NetworkPatcher
{
	public class PatchPackageQueue
	{
		public static void Patch(Logger logger, AssemblyDefinition asmCSharp)
		{
			ModuleDefinition module = asmCSharp.Modules[0];
		}
	}
}

