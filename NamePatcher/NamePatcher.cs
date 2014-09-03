using System;
using DeobfuscateMain;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace NamePatcher
{
	public class NamePatcher// : Patcher
	{
		public static string getName()
		{
			return "NameSimplifier";
		}
		public static string[] getAuthors()
		{
			return new string[]{ "DerPopo" };
		}

		public static void Patch(Logger logger, AssemblyDefinition asmCSharp, AssemblyDefinition __reserved)
		{
			foreach (ModuleDefinition mdef in asmCSharp.Modules)
			{
				logger.KeyInfo("Patching " + mdef.Types.Count + " type[s] ...");
				foreach (TypeDefinition tdef in mdef.Types)
				{
					NameNormalizer.CheckNames(tdef);
				}
			}
			NameNormalizer.FinalizeNormalizing();
			NameNormalizer.clnamestomod.Clear();
			NameNormalizer.vclasses.Clear();
		}
	}
}

