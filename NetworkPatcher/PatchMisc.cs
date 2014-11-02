using System;
using DeobfuscateMain;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System.Collections.Generic;
using System.Text;

namespace NetworkPatcher
{
	public class PatchMisc
	{
		public static void Patch(Logger logger, AssemblyDefinition asmCSharp)
		{
			ModuleDefinition module = asmCSharp.Modules[0];
			TypeDefinition entityFactory = module.GetType("EntityFactory");
			if (entityFactory == null)
			{
				logger.Error ("Unable to find EntityFactory!");
				return;
			}
			FieldDefinition entityIdDef = null;
			foreach (MethodDefinition mdef in entityFactory.Methods)
			{
				if (mdef.Name == "CreateEntity" && mdef.Parameters.Count == 1)
				{
					MethodBody body = mdef.Body;
					if (body == null)
						continue;
					for (int i = body.Instructions.Count-2; i >= 0; i--)
					{
						Instruction curInstr = body.Instructions[i];
						if (curInstr.OpCode == OpCodes.Ldfld)
						{
							FieldDefinition fdef = ((FieldReference)curInstr.Operand).Resolve();
							if (fdef.Name.Equals("id") && fdef.DeclaringType.Name.Equals("EntityCreationData"))
							{
								Instruction stfldInstr = body.Instructions [i + 1];
								if (stfldInstr.OpCode == OpCodes.Stfld)
								{
									entityIdDef = ((FieldReference)stfldInstr.Operand).Resolve();
									break;
								}
							}
						}
					}
					if (entityIdDef != null)
						break;
				}
			}
			if (entityIdDef == null)
			{
				logger.Error("Unable to find Entity.entityId!");
				return;
			}
			entityIdDef.DeclaringType.Name = "Entity";
			entityIdDef.Name = "entityId";
			logger.Info("Patched Entity.entityId.");

			MethodDefinition entityInitMdef = HelperClass.findMember<MethodDefinition>(module, entityIdDef.DeclaringType, false,
				HelperClass.MemberNameComparer<MethodDefinition> ("Init"),
				HelperClass.MethodParametersComparer ("System.Int32"),
				HelperClass.MethodOPCodeComparer( new int[]{ 0, 1, 2 },
					new OpCode[] {
						OpCodes.Ldarg_0,
						OpCodes.Ldarg_1,
						OpCodes.Stfld
					}, new object[]{null,null,null}
				)
			);
			if (entityInitMdef != null)
			{
				FieldDefinition entityClassFDef = ((FieldReference)entityInitMdef.Body.Instructions[2].Operand).Resolve();
				if (entityClassFDef == null)
					logger.Error("Unable to resolve the FieldReference to the Entity.entityClass field in Entity.Init(int)!");
				else
				{
					entityClassFDef.Name = "entityClass";
					logger.Info("Patched Entity.entityClass.");
				}
			}

			TypeDefinition gameManager = module.GetType("GameManager");
			if (gameManager == null)
			{
				logger.Error ("Unable to find GameManager!");
				return;
			}
			bool worldGetterFound = false;
			foreach (PropertyDefinition pdef in gameManager.Properties)
			{
				if (pdef.Name.Equals("World"))
				{
					MethodDefinition getter = pdef.GetMethod;
					if (getter != null)
					{
						getter.Name = "get_World";
						worldGetterFound = true;
					}
					break;
				}
			}
			if (!worldGetterFound)
			{
				logger.Error("Unable to find GameManager.get_World!");
				return;
			}
			logger.Info("Patched World.get_World.");

			//--------------------------------BlockValue------------------------------
			TypeDefinition blockChangeInfo = module.GetType("BlockChangeInfo");
			if (blockChangeInfo == null)
			{
				logger.Error("Unable to find BlockChangeInfo!");
				return;
			}
			TypeDefinition blockValue = null;
			foreach (FieldDefinition fdef in blockChangeInfo.Fields)
				if (fdef.Name.Equals("blockValue"))
					blockValue = fdef.FieldType.Resolve();
			if (blockValue == null)
			{
				logger.Error("Unable to find BlockValue!");
				return;
			}
			MethodDefinition blockValueCtor = HelperClass.findMember<MethodDefinition>(module, blockValue, false, 
				HelperClass.MemberNameComparer<MethodDefinition>(".ctor"), 
				HelperClass.MethodParametersComparer("System.UInt32"),
				HelperClass.MethodOPCodeComparer(new int[]{0,1,2}, new OpCode[]{OpCodes.Ldarg_0,OpCodes.Ldarg_1,OpCodes.Stfld}, new object[3]{null,null,null})
			);
			if (blockValueCtor != null)
			{
				((FieldReference)blockValueCtor.Body.Instructions[2].Operand).Name = "rawData";
			}
			MethodDefinition blockValueSetData = HelperClass.findMember<MethodDefinition>(module, blockValue, false, 
				HelperClass.MethodReturnTypeComparer(blockValue.Name), 
				HelperClass.MethodParametersComparer("System.Int32", "System.Byte", "System.Byte", "System.Byte", "System.Byte")
			);
			if (blockValueSetData != null)
			{
				blockValueSetData.Name = "SetData";
				blockValueSetData.Body.SimplifyMacros();
				int[] matches = HelperClass.FindOPCodePattern(blockValueSetData, new OpCode[]{OpCodes.Ldarg,OpCodes.Ldarg,OpCodes.Call}, 0, null);
				if (matches.Length == 0)
					logger.Error("Unable to locate the OPCode pattern in BlockValue.SetData!");
				Instruction[] instrs = blockValueSetData.Body.Instructions.ToArray();
				foreach (int index in matches)
				{
					ParameterDefinition par = (ParameterDefinition)instrs[index+1].Operand;
					string parName = par.Name.StartsWith("_") ? par.Name.Substring(1) : par.Name;
					foreach (PropertyDefinition propDef in blockValue.Properties)
					{
						if (propDef.SetMethod != null && propDef.SetMethod.Name.Equals(((MethodReference)instrs[index+2].Operand).Name))
						{
							propDef.Name = parName;
							propDef.SetMethod.Name = "set_" + parName;
							if (propDef.GetMethod != null)
								propDef.GetMethod.Name = "get_" + parName;
							logger.Info("Patched BlockValue." + parName);
							break;
						}
					}
				}
				blockValueSetData.Body.OptimizeMacros();
			}
			MethodDefinition blockValueGetItem = HelperClass.findMember<MethodDefinition>(module, blockValue, false, 
				HelperClass.MethodParametersComparer(),
				HelperClass.MethodReturnTypeComparer("ItemValue")
			);
			if (blockValueGetItem != null)
				blockValueGetItem.Name = "ToItem";
			MethodDefinition blockValueConvert = HelperClass.findMember<MethodDefinition>(module, blockValue, false, 
				HelperClass.MethodReturnTypeComparer("System.UInt32"),
				HelperClass.MethodParametersComparer("System.UInt32"),
				HelperClass.MethodAttributeComparer(MethodAttributes.Static)
			);
			if (blockValueConvert != null)
				blockValueConvert.Name = "ConvertOldRawData";
			FieldDefinition blockValueEmpty = HelperClass.findMember<FieldDefinition>(module, blockValue, false, 
				HelperClass.FieldTypeComparer(blockValue.Name),
				HelperClass.FieldAttributeComparer(FieldAttributes.Static)
			);
			if (blockValueEmpty != null)
				blockValueEmpty.Name = "Empty";
			blockValue.Name = "BlockValue";

			//--------------------------------Helpers------------------------------
			FieldDefinition cultureInfoField = HelperClass.findMember<FieldDefinition> (module, null, false,
				HelperClass.FieldTypeComparer ("System.Globalization.CultureInfo"),
				HelperClass.FieldAttributeComparer(FieldAttributes.Private | FieldAttributes.Static)
			);
			if (cultureInfoField != null)
			{
				TypeDefinition helpersClass = cultureInfoField.DeclaringType;
				helpersClass.Name = "Helpers";
				HelperClass.executeActions<FieldDefinition> (module, helpersClass,
					new Func<FieldDefinition, bool>[]{HelperClass.FieldTypeComparer ("System.String"),
						HelperClass.FieldAttributeComparer(FieldAttributes.Public | FieldAttributes.Static)},
					HelperClass.MemberNameSetter<FieldDefinition>("models_key")
				);
				HelperClass.executeActions<FieldDefinition> (module, helpersClass,
					new Func<FieldDefinition, bool>[]{HelperClass.FieldTypeComparer ("System.Collections.Generic.Dictionary<System.String,System.String>"),
						HelperClass.FieldAttributeComparer(FieldAttributes.Public | FieldAttributes.Static)},
					HelperClass.MemberNameSetter<FieldDefinition>("modelsByXml")
				);
				HelperClass.executeActions<MethodDefinition>(module, helpersClass,
					new Func<MethodDefinition, bool>[]{
						HelperClass.MethodParametersComparer("System.String","System.String"),
						HelperClass.MethodReturnTypeComparer("System.String"),
						HelperClass.MethodAttributeComparer(MethodAttributes.Public | MethodAttributes.Static),
						HelperClass.MethodOPCodeComparer(
							new int[]{0,1,2,3,4,5}, 
							new OpCode[]{OpCodes.Newobj,OpCodes.Stloc_0,OpCodes.Ldc_I4_0,OpCodes.Stloc_1,OpCodes.Br,OpCodes.Ldloc_0},
							null)},
					HelperClass.MemberNameSetter<MethodDefinition>("decrypt")
				);
				HelperClass.executeActions<MethodDefinition> (module, helpersClass,
					new Func<MethodDefinition, bool>[]{
						HelperClass.MethodParametersComparer("System.String"),
						HelperClass.MethodReturnTypeComparer("System.String"),
						HelperClass.MethodAttributeComparer(MethodAttributes.Public | MethodAttributes.Static),
						HelperClass.MethodOPCodeComparer(
							new int[]{0,1,2}, 
							new OpCode[]{OpCodes.Ldarg_0,OpCodes.Ldstr, OpCodes.Call},
							new object[]{null,"X",null})},
					HelperClass.MemberNameSetter<MethodDefinition>("decrypt")
				);
			}

			//-------------------------------Factories------------------------------

			MethodDefinition getTableMethod = HelperClass.findMember<MethodDefinition> (module, null, false,
				HelperClass.MemberNameComparer<MethodDefinition>("getTable"),
				HelperClass.MethodReturnTypeComparer("System.Object[]"),
				HelperClass.MethodAttributeComparer(MethodAttributes.Virtual | MethodAttributes.NewSlot)
			);
			if (getTableMethod != null)
			{
				TypeDefinition getTableType = getTableMethod.DeclaringType;
				foreach (TypeDefinition tdef in module.Types)
				{
					if (tdef.BaseType != null && tdef.BaseType.FullName.Equals(getTableType.FullName))
					{
						MethodDefinition cctorMdef = HelperClass.findMember<MethodDefinition>(module, tdef, false,
                        	HelperClass.MemberNameComparer<MethodDefinition> (".cctor"));
						if (cctorMdef != null)
						{
							MethodBody body = cctorMdef.Body;
							body.SimplifyMacros();
							int[] typeofs = HelperClass.FindOPCodePattern(cctorMdef, 
								new OpCode[]{ OpCodes.Dup, OpCodes.Ldc_I4, OpCodes.Ldtoken, OpCodes.Call }, 0);
							int[] charArrs = HelperClass.FindOPCodePattern (cctorMdef,
				                 new OpCode[]{ OpCodes.Newarr, OpCodes.Dup, OpCodes.Ldtoken, OpCodes.Call }, 0);
							if (typeofs.Length != charArrs.Length || typeofs.Length == 0)
							{
								logger.Error("(parse factory data) Unable to find the patterns in " + cctorMdef.FullName + "!");
								NetworkPatcher.error++;
							}
							else
							{
								for (int i = 0; i < typeofs.Length; i++)
								{
									//logger.Info ("_" + (typeofs [i] + 2) + "_" + body.Instructions.Count);
									//logger.Info(body.Instructions[typeofs[i]+2].Operand.ToString());
									TypeDefinition typeToGen = ((TypeReference)body.Instructions[typeofs[i]+2].Operand).Resolve();
									//logger.Info(body.Instructions[charArrs[i]+2].Operand.ToString());
									FieldDefinition typeName = ((FieldReference)body.Instructions[charArrs[i]+2].Operand).Resolve();
									typeToGen.Name = sdtd_decrypt(typeName.InitialValue);
									logger.Info ("Renamed " + typeToGen.FullName + ".");
									NetworkPatcher.success++;
								}
							}
							body.OptimizeMacros();
						}
					}
				}
			}


			//-----------------------------TileEntities-----------------------------
			TileEntityPatcher.Patch(logger, asmCSharp);
			//--------------------------AuthenticatePlayer--------------------------

			HelperClass.executeActions<MethodDefinition>(module, "GameManager", new Func<MethodDefinition, bool>[]{
				HelperClass.MethodParametersComparer("UnityEngine.NetworkView", "UnityEngine.NetworkPlayer", "System.String"),
				HelperClass.MethodReturnTypeComparer("System.Void"),
				HelperClass.MethodOPCodeComparer(new int[]{1,4,8,9,10,11,12}, 
					new OpCode[]{OpCodes.Ldsfld,OpCodes.Newarr,OpCodes.Stelem_Ref,OpCodes.Callvirt,OpCodes.Ldarg_2,OpCodes.Ldc_I4_1,OpCodes.Call},
					null)
			}, HelperClass.MemberNameSetter<MethodDefinition>("DenyPlayer"));

			HelperClass.executeActions<MethodDefinition>(module, "GameManager", new Func<MethodDefinition, bool>[]{
				HelperClass.MethodParametersComparer("UnityEngine.NetworkViewID", "UnityEngine.NetworkPlayer", "System.String", "System.String", "System.String", "System.String"),
				HelperClass.MethodReturnTypeComparer("System.Void"),
				HelperClass.MethodAttributeComparer(MethodAttributes.Public)
			}, HelperClass.MemberNameSetter<MethodDefinition>("AuthenticatePlayer"));
		}

		private static string sdtd_decrypt(byte[] _data)
		{
			char[] data = new char[_data.Length / 2];
			for (int i = 0; i < (_data.Length / 2); i++)
			{
				data[i] = (char)(((char)_data[i * 2]) | ((char)_data[i * 2 + 1] << 8));
			}
			byte[] bytes = Convert.FromBase64CharArray(data, 0, data.Length);
			return sdtd_decrypt(Encoding.UTF8.GetString(bytes), "X");
		}
		private static string sdtd_decrypt(string text, string key)
		{
			StringBuilder builder = new StringBuilder();
			for (int i = 0; i < text.Length; i++)
			{
				builder.Append((char)(text[i] ^ key[i % key.Length]));
			}
			return builder.ToString();
		}
	}
}

