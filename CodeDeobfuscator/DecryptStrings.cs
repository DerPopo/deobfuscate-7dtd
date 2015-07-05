using System;
using DeobfuscateMain;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.IO;
using System.Reflection;
using System.Collections.Generic;

namespace CodeDeobfuscator
{
	public class DecryptStrings
	{
		/*static Assembly LoadFromSameFolder(object sender, ResolveEventArgs args)
		{
			string folderPath = Deobfuscator.sourceAssemblyPath.path;
			string assemblyPath = Path.Combine(folderPath, new AssemblyName(args.Name).Name + ".dll");
			if (!File.Exists(assemblyPath))
				return null;
			Assembly assembly = Assembly.LoadFrom(assemblyPath);
			return assembly;
		}*/
		private static readonly Dictionary<Code,Func<object,int>> loadIntegerCodes = new Dictionary<Code,Func<object,int>>()
		{
			{Code.Ldc_I4, operand => {return (int)operand;}},
			{Code.Ldc_I4_S, operand => {return (int)(sbyte)operand;}},
			{Code.Ldc_I4_0, operand => {return 0;}},
			{Code.Ldc_I4_1, operand => {return 1;}},
			{Code.Ldc_I4_2, operand => {return 2;}},
			{Code.Ldc_I4_3, operand => {return 3;}},
			{Code.Ldc_I4_4, operand => {return 4;}},
			{Code.Ldc_I4_5, operand => {return 5;}},
			{Code.Ldc_I4_6, operand => {return 6;}},
			{Code.Ldc_I4_7, operand => {return 7;}},
			{Code.Ldc_I4_8, operand => {return 8;}},
		};
		public static void Apply(ModuleDefinition module, Logger logger)
		{
			//AppDomain currentDomain = AppDomain.CurrentDomain;
			//currentDomain.AssemblyResolve += new ResolveEventHandler(LoadFromSameFolder);
			Assembly assembly = Assembly.LoadFrom(Deobfuscator.sourceAssemblyPath.path + Path.DirectorySeparatorChar + Deobfuscator.sourceAssemblyPath.filename);

			Module target = assembly.GetModules()[0];
			logger.KeyInfo("Looking for string decryption calls...");
			int decryptStats = 0;
			List<MethodDefinition> decryptMethods = new List<MethodDefinition>();
			MethodDefinition[] mdefs = HelperClass.findMembers<MethodDefinition>(module, null, true);
			for (int __i = 0; __i < mdefs.Length; __i++)
			{
				MethodDefinition mdef = mdefs[__i];
				if (mdef.HasBody)
				{
					Mono.Cecil.Cil.MethodBody mdefBody = mdef.Body;
					for (int i = 0; i < (mdefBody.Instructions.Count-1); i++) {
						Instruction instr1 = mdefBody.Instructions[i];
						Instruction instr2 = mdefBody.Instructions[i+1];
						if (loadIntegerCodes.ContainsKey(instr1.OpCode.Code) && 
							(instr2.OpCode == OpCodes.Call))
						{
							int key = loadIntegerCodes[instr1.OpCode.Code](instr1.Operand);//(int)instr1.Operand;
							MethodDefinition targetMethod = ((MethodReference)instr2.Operand).Resolve();
							if (targetMethod != null && targetMethod.IsStatic &&
								targetMethod.Parameters.Count == 1 && targetMethod.ReturnType.FullName.Equals("System.String") &&
								targetMethod.HasBody && targetMethod.Body.Instructions.Count > 5 && targetMethod.Body.Instructions[4].OpCode == OpCodes.Ldelem_U1)
							{
								Type decryptorType = null;
								MethodInfo decryptorMethod = null;
								try
								{
									decryptorType = target.GetType(targetMethod.DeclaringType.FullName);
									decryptorMethod = decryptorType.GetMethod(targetMethod.Name, BindingFlags.Static | BindingFlags.NonPublic);
									string decrypted = (string)decryptorMethod.Invoke(null, new object[]{ key });
									Instruction newInstr;
									ILProcessor proc = mdefBody.GetILProcessor();
									HelperClass.SafeInsertBefore(proc, instr1, (newInstr = mdefBody.GetILProcessor().Create(OpCodes.Ldstr, decrypted)));
									//mdefBody.GetILProcessor().InsertBefore(instr1, (newInstr = mdefBody.GetILProcessor().Create(OpCodes.Ldstr, decrypted)));
									HelperClass.PatchInstructionReferences(mdefBody, instr1, newInstr);
									for (int _i = 0; _i < 2; _i++)
										HelperClass.SafeRemove(proc, mdefBody.Instructions[i+1]);
										//mdefBody.Instructions.RemoveAt(i+1);
									if (!decryptMethods.Contains(targetMethod))
										decryptMethods.Add(targetMethod);

									//logger.Info(decrypted);
									//i++;
									decryptStats++;
								}
								catch (Exception e)
								{
									logger.Warning("Unable to decrypt " + targetMethod.Name + " (" + key + ") :\r\n" + e.ToString ());
									continue;
								}
							}
						}
					}
				}
				if ((__i % (mdefs.Length/10)) == 0 && __i > 0)
					logger.KeyInfo("Decrypted strings from method #" + (__i + 1) + ".");
			}
			for (int i = 0; i < decryptMethods.Count; i++)
			{
				MethodDefinition mdef = decryptMethods[i];
				//module.Types.Remove(mdef.DeclaringType);
				mdef.DeclaringType.Name = "decryptor" + i;
				mdef.Name = "Decrypt";
			}
			logger.KeyInfo("Finished decrypting " + decryptStats + " strings!");
		}
	}
}

