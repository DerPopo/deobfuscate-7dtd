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
			HelperClass.SetLogger(logger);
			TypeDefinition packageClass = null;
			foreach (ModuleDefinition mdef in asmCSharp.Modules)
			{
				foreach (TypeDefinition tdef in mdef.Types)
				{
					foreach (FieldDefinition fdef in tdef.Fields)
					{
						if (fdef.Name.Equals ("m_PackageTypeToClass")) {
							packageClass = tdef;
							break;
						}
					}
					if (packageClass != null)
						break;
				}
				if (packageClass != null)
					break;
			}
			if (packageClass == null) {
				logger.Info("Cannot find m_PackageTypeToClass!");
				return;
			}
			logger.Info("Found m_PackageTypeToClass!");
			packageClass.Name = "Package";
			foreach (MethodDefinition mdef in packageClass.Methods) {
				if (mdef.Name.Equals (".cctor")) {
					cctorMDef = mdef;
					continue;
				}
				if (mdef.IsStatic && mdef.ReturnType.Resolve ().Equals (packageClass) && mdef.Parameters.Count == 1) {
					if (string.IsNullOrEmpty(mdef.Parameters[0].ParameterType.Namespace)) {
						(packageTypeEnumDef = mdef.Parameters[0].ParameterType.Resolve()).Name = "PackageType";
						mdef.Name = "CreatePackage";
						continue;
					}
				}
			}
			if (cctorMDef == null) {
				logger.Error("Cannot find Package.cctor()!");
				return;
			}
			logger.Info("Found Package.cctor()!");
			if (packageTypeEnumDef == null) {
				logger.Error("Cannot find CreatePackage!");
				return;
			}
			logger.Info("Found CreatePackage!");

			bool curFound = false;
			foreach (MethodDefinition mdef in packageClass.Methods) {
				if (mdef.IsStatic && mdef.Parameters.Count == 1 && mdef.ReturnType.Resolve().Equals(packageTypeEnumDef) && mdef.Parameters[0].ParameterType.Resolve().FullName.Equals("System.IO.BinaryReader"))
				{
					mdef.Name = "ReadPackageType";
					curFound = true;
					success++;
					break;
				}
			}
			if (!curFound) {
				logger.Warning("Cannot find ReadPackageType!");
				error++;
			}

			logger.Info("Found ReadPackageType!");
			PatchVirtualPackageMethods(packageClass, logger, true);

			MethodBody cctorBody = cctorMDef.Body;
			cctorBody.SimplifyMacros();
			for (int i = 1; i < cctorBody.Instructions.Count; i++)
			{
				Instruction curInstr = cctorBody.Instructions[i];
				if (curInstr.OpCode == OpCodes.Ldtoken)
				{
					if (typeof(TypeReference).IsAssignableFrom(curInstr.Operand.GetType()))
					{
						TypeReference curPackageClass = (TypeReference)curInstr.Operand;
						Instruction lastInstr = cctorBody.Instructions[i - 1];
						if (lastInstr.OpCode == OpCodes.Ldc_I4)
						{
							FieldDefinition enumField = null;
							int enumFieldId = (Int32)lastInstr.Operand;
							logger.Info("Analyzing packet class (" + curPackageClass.FullName + "; " + enumFieldId + ")...");
							foreach (FieldDefinition curEnumField in packageTypeEnumDef.Fields)
							{
								if (curEnumField.HasConstant && curEnumField.Constant != null)
								{
									//logger.Log (curEnumField.Constant.GetType ().FullName);
									int curConst = -1;
									if (curEnumField.Constant.GetType () == typeof(Byte))
										curConst = (int)(((Byte)curEnumField.Constant));
									else if (curEnumField.Constant.GetType () == typeof(Int16))
										curConst = (int)(((Int16)curEnumField.Constant));
									else if (curEnumField.Constant.GetType () == typeof(Int32))
										curConst = (int)(((Int32)curEnumField.Constant));
									if (curConst == -1)
										logger.Info("Unknown const in packageTypeEnumDef.Fields!");
									else if (curConst == enumFieldId)
									{
										enumField = curEnumField;
										break;
									}
								}
							}
							if (enumField == null) {
								logger.Warning("The package class uses an unknown PackageType!");
								curPackageClass.Name = "NetPackage_" + enumFieldId;
							} else if (Helpers.isObfuscated(enumField.Name)) {
								MethodDefinition ctorMdef = HelperClass.findMember<MethodDefinition>(packageClass.Module, curPackageClass.Resolve(), false, false,
									HelperClass.MemberNameComparer<MethodDefinition>(".ctor"),
									HelperClass.MethodParametersComparer(""));
								if (ctorMdef != null && ctorMdef.Parameters [0].Name.Equals("_te"))
								{
									curPackageClass.Name = "NetPackage_TileEntityUpdate";
									enumField.Name = "TileEntityUpdate";
								}
								else
								{
									logger.Info("The package class uses an obfuscated PackageType!");
									curPackageClass.Name = "NetPackage_" + enumFieldId;
								}
							}
							else
								curPackageClass.Name = "NetPackage_" + enumField.Name;
							PatchVirtualPackageMethods(curPackageClass.Resolve(), logger, false);
							logger.Info("Renamed packet class (" + curPackageClass.FullName + ")!");
						}
						else
							logger.Warning("There is no ldc.i4 before the current ldtoken !");
					}
					else
						logger.Warning("A Ldtoken instruction has no TypeReference operand!");
				}
			}
			cctorBody.OptimizeMacros();
// PackageQueue is now based on an interface that's not obfuscated, only private stuff of the actual implementation is obfuscated
//			PatchPackageQueue.Patch(logger, asmCSharp);

			PatchMisc.Patch(logger, asmCSharp);
			logger.Log(Logger.Level.KEYINFO, String.Format("Successful: {0} / Failed: {1}", success, error));
		}

		private static void PatchVirtualPackageMethods(TypeDefinition tdef, Logger logger, bool isBaseClass)
		{
			Dictionary<String, Boolean> methodPatchedByName = new Dictionary<String, Boolean>();
			methodPatchedByName.Add("GetPackageType", false);
			methodPatchedByName.Add("Read", false);
			methodPatchedByName.Add("Write", false);
			methodPatchedByName.Add("Process", false);
			methodPatchedByName.Add("GetEstimatedPackageSize", false);
//			methodPatchedByName.Add("SetChannel", !isBaseClass);
			methodPatchedByName.Add("GetFriendlyName", !isBaseClass);
			foreach (MethodDefinition mdef in tdef.Methods) {
				if (!mdef.IsGetter && mdef.IsVirtual && mdef.Parameters.Count == 0 && mdef.ReturnType.Resolve().Equals(packageTypeEnumDef))
				{
					mdef.Name = "GetPackageType";
					logger.Info("Found " + mdef.FullName + "!");
					methodPatchedByName["GetPackageType"] = true;
					success++;
					continue;
				}
//				if (mdef.IsVirtual && mdef.Parameters.Count == 1 && mdef.ReturnType.Resolve().FullName.Equals("System.Void") &&
//					mdef.Parameters[0].ParameterType.Resolve().FullName.Equals("System.Int32"))
//				{
//					mdef.Name = "SetChannel";
//					logger.Info("Found " + mdef.FullName + "!");
//					methodPatchedByName["SetChannel"] = true;
//					success++;
//					continue;
//				}
				if (mdef.IsVirtual && mdef.Parameters.Count == 1 && mdef.ReturnType.Resolve().FullName.Equals("System.Void") &&
					mdef.Parameters[0].ParameterType.Resolve().FullName.Equals("System.IO.BinaryReader"))
				{
					mdef.Name = "Read";
					logger.Info("Found " + mdef.FullName + "!");
					methodPatchedByName["Read"] = true;
					success++;
					continue;
				}
				if (mdef.IsVirtual && mdef.Parameters.Count == 1 && mdef.ReturnType.Resolve().FullName.Equals("System.Void") &&
					mdef.Parameters[0].ParameterType.Resolve().FullName.Equals("System.IO.BinaryWriter"))
				{
					mdef.Name = "Write";
					logger.Info("Found " + mdef.FullName + "!");
					methodPatchedByName["Write"] = true;
					success++;
					continue;
				}
				if (mdef.IsVirtual && mdef.Parameters.Count == 2 && mdef.ReturnType.Resolve().FullName.Equals("System.Void") &&
					mdef.Parameters[0].ParameterType.Resolve().FullName.Equals("World") &&
					mdef.Parameters[1].ParameterType.Resolve().FullName.Equals("INetConnectionCallbacks"))
				{
					mdef.Name = "Process";
					logger.Info("Found " + mdef.FullName + "!");
					methodPatchedByName["Process"] = true;
					success++;
					continue;
				}
				if (!mdef.IsGetter && mdef.IsVirtual && mdef.Parameters.Count == 0 && mdef.ReturnType.Resolve().FullName.Equals("System.Int32"))
				{
					mdef.Name = "GetEstimatedPackageSize";
					logger.Info("Found " + mdef.FullName + "!");
					methodPatchedByName["GetEstimatedPackageSize"] = true;
					success++;
					continue;
				}
				if (!mdef.IsGetter && mdef.IsVirtual && mdef.Parameters.Count == 0 && mdef.ReturnType.Resolve().FullName.Equals("System.String"))
				{
					mdef.Name = "GetFriendlyName";
					logger.Info("Found " + mdef.FullName + "!");
					methodPatchedByName["GetFriendlyName"] = true;
					success++;
					continue;
				}
			}
			foreach (String mdName in methodPatchedByName.Keys) {
				if (!methodPatchedByName[mdName])
				{
					logger.Log(isBaseClass ? Logger.Level.ERROR : Logger.Level.WARNING, "Unable to find " + (tdef.Name + "." + mdName) + "! (already defined in a base class?)");
					error++;
				}
			}
		}
	}
}

