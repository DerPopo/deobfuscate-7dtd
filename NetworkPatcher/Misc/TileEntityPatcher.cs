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

				//TODO : deobfuscate TileEntity field names
				/*TypeDefinition tileEntitySecureLoot = module.GetType("TileEntitySecureLoot");
				if (tileEntitySecureLoot == null)
				{
					logger.Warning ("Unable to patch TileEntitySecureLoot : class not found.");
					NetworkPatcher.error++;
				}
				else
				{

					NetworkPatcher.success++;
				}*/
			}
		}
	}
}

