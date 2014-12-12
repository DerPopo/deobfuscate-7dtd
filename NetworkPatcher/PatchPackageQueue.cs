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
			TypeDefinition connectionMgr = module.GetType("ConnectionManager");
			if (connectionMgr == null)
			{
				logger.Error ("Unable to find ConnectionManager!");
				return;
			}
			TypeDefinition packetQueueDef = null;
			foreach (MethodDefinition mdef in connectionMgr.Methods)
			{
				if (mdef.Name == "SetNetConnection" && mdef.Parameters.Count == 3)
				{
					if (Helpers.isObfuscated(mdef.Parameters[2].ParameterType.Resolve().Name))
					{
						packetQueueDef = mdef.Parameters[2].ParameterType.Resolve();
						break;
					}
				}
			}
			if (packetQueueDef == null)
			{
				logger.Error("Unable to find ConnectionManager.SetNetConnection!");
				return;
			}
			packetQueueDef.Name = "PackageQueue";


			Dictionary<String, Boolean> memberPatchedByName = new Dictionary<String, Boolean>();
			memberPatchedByName.Add("Send", false);
			memberPatchedByName.Add("SendAll", false);
			memberPatchedByName.Add("Update", false);
			memberPatchedByName.Add("GetEstimatedStreamSize", false);
			memberPatchedByName.Add("Read", false);
			memberPatchedByName.Add("IsConnected", false);
			memberPatchedByName.Add("Close", false);
			memberPatchedByName.Add("isClosed", false);
			memberPatchedByName.Add("emptyList", false);
			memberPatchedByName.Add("lastBytesSent", false);

			foreach (MethodDefinition mdef in packetQueueDef.Methods)
			{
				if (!memberPatchedByName["Send"] &&
				    mdef.IsVirtual && mdef.ReturnType.FullName.Equals("System.Void") &&
				    mdef.Parameters.Count == 1 && mdef.Parameters[0].ParameterType.FullName.Equals("Package"))
				{
					Helpers.PatchVMethod(mdef, "Send");
					memberPatchedByName["Send"] = true;
				}
				else if (!memberPatchedByName["SendAll"] &&
					mdef.IsVirtual && mdef.ReturnType.FullName.Equals("System.Void") &&
					mdef.Parameters.Count == 1 && mdef.Parameters[0].ParameterType.FullName.StartsWith("System.Collections.Generic.List"))
				{
					Helpers.PatchVMethod(mdef, "SendAll");
					memberPatchedByName["SendAll"] = true;
				}
				else if (!memberPatchedByName["Update"] &&
					mdef.IsVirtual && mdef.ReturnType.FullName.Equals("System.Void") &&
					mdef.Parameters.Count == 0)
				{
					Mono.Collections.Generic.Collection<Instruction> instrs = mdef.Body.Instructions;
					if (instrs.Count > 5 &&
					    (
							instrs[0].OpCode == OpCodes.Ldarg_0 &&
							instrs[1].OpCode == OpCodes.Ldfld && ((FieldReference)instrs[1].Operand).Name.Equals("sendQueue") &&
							instrs[2].OpCode == OpCodes.Stloc_0 &&
							instrs[3].OpCode == OpCodes.Ldloc_0 &&
							instrs[4].OpCode == OpCodes.Call && ((MethodReference)instrs[4].Operand).Name.Equals("Enter")
						)
					)
					{
						Helpers.PatchVMethod(mdef, "Update");
						memberPatchedByName["Update"] = true;
					}
				}
				else if (!memberPatchedByName["GetEstimatedStreamSize"] &&
					mdef.IsVirtual && mdef.ReturnType.FullName.Equals("System.Int32") &&
					mdef.Parameters.Count == 0)
				{
					Helpers.PatchVMethod(mdef, "GetEstimatedStreamSize");
					memberPatchedByName["GetEstimatedStreamSize"] = true;
				}
				else if (!memberPatchedByName["Read"] &&
					mdef.IsVirtual && mdef.ReturnType.FullName.Equals("System.Void") &&
					mdef.Parameters.Count == 1 && 
					mdef.Parameters[0].ParameterType.IsArray && mdef.Parameters[0].ParameterType.FullName.Contains("System.Byte"))
				{
					Helpers.PatchVMethod(mdef, "Read");
					memberPatchedByName["Read"] = true;
				}
				else if (!memberPatchedByName["IsConnected"] &&
					mdef.IsVirtual && mdef.ReturnType.FullName.Equals("System.Boolean") &&
					mdef.Parameters.Count == 0)
				{
					Helpers.PatchVMethod(mdef, "IsConnected");
					memberPatchedByName["IsConnected"] = true;
				}
				if (!memberPatchedByName["Close"] &&
					mdef.IsVirtual && mdef.ReturnType.FullName.Equals("System.Void") &&
					mdef.Parameters.Count == 0)
				{
					Mono.Collections.Generic.Collection<Instruction> instrs = mdef.Body.Instructions;
					if (instrs.Count > 4 &&
						(
							instrs[0].OpCode == OpCodes.Ldarg_0 &&
							instrs[1].OpCode == OpCodes.Ldc_I4_1 &&
							instrs[2].OpCode == OpCodes.Volatile &&
							instrs[3].OpCode == OpCodes.Stfld
						)
					)
					{
						Helpers.PatchVMethod(mdef, "Close");
						((FieldReference)instrs[3].Operand).Name = "isClosed";
						memberPatchedByName["isClosed"] = true;
						memberPatchedByName["Close"] = true;
					}
				}
				if (!memberPatchedByName["lastBytesSent"] && mdef.Name.Equals("thread_CommWriter"))
				{
					int[] patternResults = HelperClass.FindOPCodePattern(mdef, new OpCode[] {
						OpCodes.Ldloc_S,
						OpCodes.Callvirt,
						OpCodes.Conv_I4,
						OpCodes.Add,
						OpCodes.Volatile,
						OpCodes.Stfld
					}, 5);
					if (patternResults.Length == 1)
					{
						((FieldReference)mdef.Body.Instructions[patternResults[0]].Operand).Resolve().Name = "lastBytesSent";
						memberPatchedByName ["lastBytesSent"] = true;
					}
				}
			}
			foreach (FieldDefinition fdef in packetQueueDef.Fields)
			{
				if (!memberPatchedByName["emptyList"] && fdef.IsPrivate && fdef.IsStatic && fdef.IsInitOnly && fdef.FieldType.FullName.StartsWith ("System.Collections.Generic.List"))
				{
					fdef.Name = "emptyList";
					memberPatchedByName["emptyList"] = true;
				}
				/*else if (!memberPatchedByName["lastBytesSent"] && fdef.IsPublic && !fdef.IsStatic && fdef.FieldType.FullName.Equals("System.Int64") && Helpers.isObfuscated(fdef.Name))
				{
					fdef.Name = "lastBytesSent";
					memberPatchedByName["lastBytesSent"] = true;
				}*/
			}

			foreach (string name in memberPatchedByName.Keys)
			{
				if (!memberPatchedByName[name])
				{
					logger.Error ("Unable to find PackageQueue." + name + "!");
					NetworkPatcher.error++;
				}
				else
				{
					logger.Info ("Patched PackageQueue." + name + ".");
					NetworkPatcher.success++;
				}
			}

			MethodDefinition getPackageQueueMdef = HelperClass.findMember<MethodDefinition>(module, connectionMgr, false, true, 
				HelperClass.MethodParametersComparer("System.Int32", "System.Int32"),
				HelperClass.MethodParameterNamesComparer("_clientId", "_channel"),
				HelperClass.MethodReturnTypeComparer(packetQueueDef));
			if (getPackageQueueMdef != null) {
				getPackageQueueMdef.Name = "GetPackageQueue";
				logger.Info("Patched ConnectionManager.GetPackageQueue.");
			}
		}
	}
}

