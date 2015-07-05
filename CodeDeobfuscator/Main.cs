using System;
using DeobfuscateMain;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace CodeDeobfuscator
{
	public class Main
	{
		public static string getName()
		{
			return "CodeDeobfuscator";
		}
		public static string[] getAuthors()
		{
			return new string[]{ "DerPopo" };
		}

		public static void Patch(Logger logger, AssemblyDefinition asmCSharp, AssemblyDefinition __reserved)
		{
			DecryptStrings.Apply(asmCSharp.Modules[0], logger);
			GarbageRemover.Apply(asmCSharp.Modules[0], logger);
		}
	}
}

