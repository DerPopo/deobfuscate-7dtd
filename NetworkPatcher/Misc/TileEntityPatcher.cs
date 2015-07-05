using System;
using DeobfuscateMain;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System.Collections.Generic;
using System.Text;

namespace NetworkPatcher
{
	public class TileEntityPatcher
	{
		public static void Patch(Logger logger, AssemblyDefinition asmCSharp)
		{
			TypeDefinition tileEntitySecureLoot = null; TypeDefinition tileEntitySecure = null;
			HelperClass.SetLogger(logger);
			ModuleDefinition module = asmCSharp.Modules[0];

			if ((tileEntitySecure = module.GetType("TileEntitySecure")) == null)
				logger.Error("Unable to find TileEntitySecure!");
			else
				DeobfuscateTileEntitySecure(module, tileEntitySecure);

			if ((tileEntitySecureLoot = module.GetType("TileEntitySecureLootContainer")) == null)
				logger.Error("Unable to find TileEntitySecureLootContainer!");
			else
				DeobfuscateTileEntitySecure(module, tileEntitySecureLoot);
		}
		private static void DeobfuscateTileEntitySecure(ModuleDefinition module, TypeDefinition tileEntityDef)
		{
			HelperClass.executeActions<FieldDefinition>(module, tileEntityDef, new HelperClass.GenericFuncContainer<FieldDefinition, bool>[]{
				HelperClass.FieldTypeComparer("System.Collections.Generic.List<System.String>")
			}, HelperClass.MemberNameSetter<FieldDefinition>("allowedUsers"));

			MethodDefinition setOwnerMethod = HelperClass.findMember<MethodDefinition>(module, tileEntityDef, false, true, 
				HelperClass.MemberNameComparer<MethodDefinition>("SetOwner"), 
				HelperClass.MethodOPCodeComparer(new int[]{ 0, 1, 2 }, new OpCode[] {OpCodes.Ldarg_0,OpCodes.Ldarg_1,OpCodes.Stfld}, null)
			);
			if (setOwnerMethod != null)
				((FieldReference)setOwnerMethod.Body.Instructions[2].Operand).Resolve().Name = "owner";

			MethodDefinition setLockedMethod = HelperClass.findMember<MethodDefinition>(module, tileEntityDef, false, true, 
				HelperClass.MemberNameComparer<MethodDefinition>("SetLocked"), 
				HelperClass.MethodOPCodeComparer(new int[]{ 0, 1, 2 }, new OpCode[] {OpCodes.Ldarg_0,OpCodes.Ldarg_1,OpCodes.Stfld}, null)
			);
			if (setLockedMethod != null)
				((FieldReference)setLockedMethod.Body.Instructions[2].Operand).Resolve().Name = "isLocked";

			MethodDefinition hasPasswordMethod = HelperClass.findMember<MethodDefinition>(module, tileEntityDef, false, true, 
				HelperClass.MemberNameComparer<MethodDefinition>("HasPassword"), 
				HelperClass.MethodOPCodeComparer(new int[]{ 0, 1, 2 }, new OpCode[] {OpCodes.Ldarg_0,OpCodes.Ldfld,OpCodes.Brfalse_S}, null)
			);
			if (hasPasswordMethod != null)
				((FieldReference)hasPasswordMethod.Body.Instructions[1].Operand).Resolve().Name = "password";
		}
	}
}

