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
			TypeDefinition tileEntitySecureLoot = null; TypeDefinition tileEntitySecureDoor = null;
			HelperClass.SetLogger(logger);
			ModuleDefinition module = asmCSharp.Modules[0];

			//(Thanks to KaXaK)
			MethodDefinition[] getTileEntityTypeMethods = HelperClass.findMembers<MethodDefinition> (module, null, 
				HelperClass.MemberNameComparer<MethodDefinition>("GetTileEntityType"),
				HelperClass.MethodReturnTypeComparer("TileEntityType"));
			TypeDefinition enumTileEntity = module.GetType("TileEntityType");
			if (enumTileEntity == null)
				logger.Error ("Unable to find the TileEntityType enum!");
			else
			{
				Dictionary<int,string> TENameByEnumId = new Dictionary<int, string>();
				foreach (FieldDefinition curEnumField in enumTileEntity.Fields)
				{
					if (curEnumField.HasConstant && curEnumField.Constant != null)
					{
						int curConst = -1;
						if (curEnumField.Constant.GetType() == typeof(Byte))
							curConst = (int)(((Byte)curEnumField.Constant));
						else if (curEnumField.Constant.GetType() == typeof(Int16))
							curConst = (int)(((Int16)curEnumField.Constant));
						else if (curEnumField.Constant.GetType() == typeof(Int32))
							curConst = (int)(((Int32)curEnumField.Constant));
						if (curConst == -1)
							logger.Info ("Unknown const in TileEntityType's fields!");
						else if (TENameByEnumId.ContainsKey(curConst))
							logger.Warning("Already found the enum value " + curConst + " in TileEntityType!");
						else
							TENameByEnumId.Add(curConst, curEnumField.Name);
					}
				}
				foreach (MethodDefinition getTileEntity in getTileEntityTypeMethods)
				{
					if (getTileEntity.HasBody)
					{
						MethodBody body = getTileEntity.Body;
						body.SimplifyMacros();
						if (body.Instructions.Count == 2 && body.Instructions[0].OpCode == OpCodes.Ldc_I4)
						{
							string teType;
							if (TENameByEnumId.TryGetValue((int)body.Instructions[0].Operand, out teType))
							{
								if (teType.Equals("None"))
									teType = "";
								getTileEntity.DeclaringType.Name = "TileEntity" + teType;
								NetworkPatcher.success++;
								logger.Info("Renamed TileEntity" + teType + ".");

								if (teType.Equals("SecureLoot"))
									tileEntitySecureLoot = getTileEntity.DeclaringType;
								else if (teType.Equals("SecureDoor"))
									tileEntitySecureDoor = getTileEntity.DeclaringType;
							}
							else
							{
								logger.Warning("The GetTileEntityType method of " + getTileEntity.DeclaringType.Name + " returns an invalid enum value!");
								NetworkPatcher.error++;
							}

						}
						else
						{
							logger.Warning("Unable to extract the name of " + getTileEntity.DeclaringType.Name + " using the GetTileEntityType method.");
							NetworkPatcher.error++;
						}
						body.OptimizeMacros();
					}
				}

				//TypeDefinition tileEntitySecureLoot = module.GetType("TileEntitySecureLoot");
				if (tileEntitySecureLoot == null)
				{
					logger.Warning ("Unable to patch TileEntitySecureLoot : class not found.");
					NetworkPatcher.error++;
				}
				else
				{
					DeobfuscateTileEntitySecure(module, tileEntitySecureLoot);
					NetworkPatcher.success++;
				}

				//TypeDefinition tileEntitySecureDoor = module.GetType("TileEntitySecureDoor");
				if (tileEntitySecureDoor == null)
				{
					logger.Warning ("Unable to patch TileEntitySecureDoor : class not found.");
					NetworkPatcher.error++;
				}
				else
				{
					TypeDefinition tileEntitySecureDoorBase = tileEntitySecureDoor.BaseType.Resolve();
					if (Helpers.isObfuscated(tileEntitySecureDoorBase.Name))
						tileEntitySecureDoorBase.Name = "TileEntitySecureDoorBase";
					DeobfuscateTileEntitySecure(module, tileEntitySecureDoorBase);
					NetworkPatcher.success++;
				}
			}
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
				HelperClass.MethodOPCodeComparer(new int[]{ 0, 1, 2 }, new OpCode[] {OpCodes.Ldarg_0,OpCodes.Ldfld,OpCodes.Brfalse}, null)
			);
			if (hasPasswordMethod != null)
				((FieldReference)hasPasswordMethod.Body.Instructions[1].Operand).Resolve().Name = "password";
		}
	}
}

