using System;
using DeobfuscateMain;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System.IO;
using System.Collections.Generic;

namespace CodeDeobfuscator
{
	public class GarbageRemover
	{
		public static void Apply(ModuleDefinition module, Logger logger)
		{
			int stat_obfCall = 0, stat_nullField = 0, stat_duppop = 0, stat_obfSwitch = 0, stat_obfIf = 0, stat_obfClasses = 0;

			List<MethodDefinition> decryptMethods = new List<MethodDefinition>();
			MethodDefinition[] mdefs = HelperClass.findMembers<MethodDefinition>(module, null, true);
			for (int methodIndex = 0; methodIndex < mdefs.Length; methodIndex++)
			{
				MethodDefinition mdef = mdefs[methodIndex];
				if (mdef.HasBody)
				{
					MethodBody mdefBody = mdef.Body;
					for (int i = 0; i < mdefBody.Instructions.Count; i++)
					{
						Instruction instr = mdefBody.Instructions[i];
						if (instr.OpCode == OpCodes.Call)
						{
							MethodReference mref = (MethodReference)instr.Operand;
							MethodDefinition targetMethod = mref.Resolve();
							if (mdef.DeclaringType.Name.Equals("GameManager") && mdef.Name.Equals("EO"))
							{
								mref.Resolve ();
							}
							//indicates that this is a method by the obfuscator that does simple operations which non-obfuscated methods wouldn't call
							if (targetMethod != null &&
								targetMethod.IsStatic && targetMethod.Name.Equals("W") && targetMethod.DeclaringType.Namespace.Equals("A")/* &&
								(targetMethod.Attributes & (MethodAttributes.Private | MethodAttributes.Public)) == 0 &&
								targetMethod.IsAssembly*/)
							{
								//logger.Info(targetMethod.DeclaringType.FullName + "::" + targetMethod.Name + " -> " + mdef.DeclaringType.FullName + "::" + mdef.Name + " (@" + i + ")");
								MethodBody targetBody = targetMethod.Body;
								targetBody.SimplifyMacros();
								bool canPatch = (targetBody.Instructions.Count > targetMethod.Parameters.Count) && !targetBody.HasVariables;
								if (canPatch)
								{
									for (int k = 0; k < targetMethod.Parameters.Count; k++)
									{
										if (targetBody.Instructions[k].OpCode != OpCodes.Ldarg || ((ParameterDefinition)targetBody.Instructions[k].Operand).Index != k)
										{
											canPatch = false;
											break;
										}
									}
									if (canPatch)
									{
										for (int k = targetMethod.Parameters.Count; k < targetBody.Instructions.Count; k++)
										{
											if (targetBody.Instructions[k].Operand is ParameterDefinition)
											{
												canPatch = false;
												break;
											}
										}
									}
								}
								if (!canPatch)
								{
									logger.Warning("Cannot reimport the instructions from " + targetMethod.FullName + " (not yet supported)!");
									continue;
								}
								ILProcessor proc = mdefBody.GetILProcessor();
								List<Instruction> targetInstructions = new List<Instruction>(targetBody.Instructions.Count-targetMethod.Parameters.Count);
								int _k = 0; Instruction before = instr;
								//create blank instructions to simplify creating jumps (the instruction and the operand will be changed afterwards)
								for (int k = targetMethod.Parameters.Count; k < targetBody.Instructions.Count; k++, _k++)
								{
									Instruction nopInstr = proc.Create(OpCodes.Nop);
									targetInstructions.Add(nopInstr);
									HelperClass.SafeInsertAfter(proc, before, nopInstr);
									//proc.InsertAfter(before, nopInstr);
									before = nopInstr;
								}
								_k = 0;
								for (int k = targetMethod.Parameters.Count; k < targetBody.Instructions.Count; k++, _k++)
								{
									Instruction curInstr = targetBody.Instructions[k];
									switch (curInstr.OpCode.Code)
									{
										case Mono.Cecil.Cil.Code.Ret://OpCodes.Ret.Code:
											HelperClass.SafeRemove(proc, targetInstructions[_k]);
											//proc.Remove(targetInstructions[_k]);
											targetInstructions.RemoveAt(_k);
											break;
										default:
											targetInstructions[_k].OpCode = curInstr.OpCode;
											if (curInstr.Operand is Instruction)
											{
												Instruction targetedInstruction = (Instruction)curInstr.Operand;
												int instrIndex = -1; int targetInstrIndex = -1;
												for (int l = targetMethod.Parameters.Count; l < targetBody.Instructions.Count; l++)
												{
													if (targetBody.Instructions[l].Equals(targetedInstruction))
													{
														instrIndex = l;
														targetInstrIndex = l - targetMethod.Parameters.Count;
														break;
													}
												}
												if (instrIndex == -1)
													throw new Exception("Unable to find the target of instruction #" + k + " in " + targetMethod.FullName + "!");
												if (targetBody.Instructions[instrIndex].OpCode == OpCodes.Ret)
													targetInstructions[_k].Operand = targetInstructions[targetInstructions.Count-1].Next;
												else
													targetInstructions[_k].Operand = targetInstructions[targetInstrIndex];
											}
											else if (curInstr.Operand is Instruction[])
											{
												Instruction[] oldTargetList = (Instruction[])curInstr.Operand;
												Instruction[] newInstructions = new Instruction[oldTargetList.Length];
												for (int l = 0; l < oldTargetList.Length; l++)
												{
													Instruction targetedInstruction = oldTargetList[l];
													int instrIndex = -1; int targetInstrIndex = -1;
													for (int m = targetMethod.Parameters.Count; m < targetBody.Instructions.Count; m++)
													{
														if (targetBody.Instructions[l].Equals(targetedInstruction))
														{
															instrIndex = l;
															targetInstrIndex = l - targetMethod.Parameters.Count;
															break;
														}
													}
													if (instrIndex == -1)
														throw new Exception("Unable to find the target of instruction #" + k + " in " + targetMethod.FullName + "!");
													if (targetBody.Instructions[instrIndex].OpCode == OpCodes.Ret)
														newInstructions[l] = targetInstructions[targetInstructions.Count-1].Next;
													else
														newInstructions[l] = targetInstructions[targetInstrIndex];
												}
												targetInstructions[_k].Operand = newInstructions;
											}
											else if (curInstr.Operand is MethodReference)
											{
												targetInstructions[_k].Operand = curInstr.Operand;//module.Import(((MethodReference)curInstr.Operand).Resolve());
											}
											else if (curInstr.Operand is FieldReference)
											{
												targetInstructions[_k].Operand = curInstr.Operand;//module.Import(((FieldReference)curInstr.Operand).Resolve());
											}
											else if (curInstr.Operand is TypeReference)
											{
												targetInstructions[_k].Operand = curInstr.Operand;//module.Import(((TypeReference)curInstr.Operand).Resolve());
											}
											else if (curInstr.Operand is IMetadataTokenProvider)
											{
												throw new Exception("Unsupported instruction type IMetadataTokenProvider in " + targetMethod.FullName + "!");
												//IMetadataTokenProvider provider = (IMetadataTokenProvider)curInstr.Operand;
												//string name = provider.MetadataToken.ToString();
												//targetInstructions[_k].Operand = null;//module.Import(((TypeReference)curInstr.Operand).Resolve());
											}
											else
											{
												targetInstructions[_k].Operand = curInstr.Operand;
											}
											break;
									}
								}

								HelperClass.PatchInstructionReferences(mdefBody, instr, (targetInstructions.Count > 0) ? targetInstructions[0] : instr.Next);
								HelperClass.SafeRemove(proc, instr);
								i--;

								stat_obfCall++;
							}
						}
						else if (instr.OpCode == OpCodes.Ldsfld)
						{
							FieldReference fref = (FieldReference)instr.Operand;
							FieldDefinition targetField = fref.Resolve();
							if (targetField != null && targetField.Name.Equals("W") && targetField.DeclaringType.Namespace.Equals("A") && targetField.IsAssembly)
							{
								if (targetField.DeclaringType.Fields.Count == 1 && targetField.DeclaringType.Methods.Count == 0)
								{
									ILProcessor proc = mdefBody.GetILProcessor();
									Instruction newInstr = proc.Create(OpCodes.Ldnull);
									HelperClass.PatchInstructionReferences(mdefBody, instr, newInstr);
									HelperClass.SafeInsertAfter(proc, instr, newInstr);
									HelperClass.SafeRemove(proc, instr);
									stat_nullField++;
								}
							}
						}
						else if ((instr.OpCode == OpCodes.Dup) && ((i+1) < mdefBody.Instructions.Count) && (mdefBody.Instructions[i+1].OpCode == OpCodes.Pop))
						{
							ILProcessor proc = mdefBody.GetILProcessor();
							HelperClass.PatchInstructionReferences(mdefBody, instr, mdefBody.Instructions[i+2]);
							HelperClass.SafeRemove(proc, mdefBody.Instructions[i+1]);
							HelperClass.SafeRemove(proc, instr);
							i--;
							stat_duppop++;
						}
						else if (i > 0 && 
							(instr.OpCode == OpCodes.Switch) && 
							((Instruction[])instr.Operand).Length == 1 && ((Instruction[])instr.Operand)[0] == mdefBody.Instructions[i-1])
						{
							Instruction ldInstr = mdefBody.Instructions[i-1];
							HelperClass.PatchInstructionReferences(mdefBody, ldInstr, mdefBody.Instructions[i+1]);
							ILProcessor proc = mdefBody.GetILProcessor();
							HelperClass.SafeRemove(proc, mdefBody.Instructions[i-1]);
							HelperClass.SafeRemove(proc, instr);
							i -= 2;
							stat_obfSwitch++;
						}
						else if ((instr.OpCode == OpCodes.Ldc_I4_1) && ((i+4) < mdefBody.Instructions.Count) &&
							(mdefBody.Instructions[i+1].OpCode.Code == Code.Brtrue_S) &&
							(mdefBody.Instructions[i+2].OpCode.Code == Code.Ldtoken) &&
							(mdefBody.Instructions[i+3].OpCode.Code == Code.Pop)
						)
						{
							Instruction ldInstr = mdefBody.Instructions[i-1];
							HelperClass.PatchInstructionReferences(mdefBody, instr, mdefBody.Instructions[i+4]);
							ILProcessor proc = mdefBody.GetILProcessor();
							for (int k = 0; k < 4; k++)
								HelperClass.SafeRemove(proc, mdefBody.Instructions[i]);
							i--;
							stat_obfIf++;
						}
					}
				}
				if ((methodIndex % (mdefs.Length/10)) == 0 && methodIndex > 0)
					logger.KeyInfo("Removed garbage from method #" + (methodIndex + 1) + ".");
			}
			List<TypeDefinition> referencedTypes = new List<TypeDefinition>();

			for (int methodIndex = 0; methodIndex < mdefs.Length; methodIndex++)
			{
				MethodDefinition mdef = mdefs[methodIndex];
				if (mdef.HasBody)
				{
					MethodBody mdefBody = mdef.Body;
					for (int k = 0; k < mdefBody.Instructions.Count; k++)
					{
						Instruction instr = mdefBody.Instructions[k];
						if (instr.Operand is MethodReference)
						{
							MethodDefinition targetMethod = ((MethodReference)instr.Operand).Resolve();
							if (targetMethod != null)
							{
								referencedTypes.Add(targetMethod.DeclaringType);
							}
						}
						else if (instr.Operand is FieldReference)
						{
							FieldDefinition targetField = ((FieldReference)instr.Operand).Resolve();
							if (targetField != null)
							{
								referencedTypes.Add(targetField.DeclaringType);
							}
						}
						else if (instr.Operand is TypeReference)
						{
							TypeDefinition targetType = ((TypeReference)instr.Operand).Resolve();
							if (targetType != null)
							{
								referencedTypes.Add(targetType);
							}
						}
					}
				}
			}
			for (int i = module.Types.Count-1; i >= 0; i--)
			{
				TypeDefinition tdef = module.Types[i];
				if (tdef.Namespace.Equals("A") && !tdef.IsEnum && !tdef.Name.Equals("AssemblyInfoAttribute"))
				{
					if (!referencedTypes.Contains(tdef))
					{
						module.Types.RemoveAt(i);
						stat_obfClasses++;
					}
				}
			}
			logger.KeyInfo("Removed " + 
				stat_obfCall + " extracted method calls, " + 
				stat_nullField + " always null fields, " + 
				stat_duppop + " senseless dup/pop, " + 
				stat_obfSwitch + " senseless switches, " + 
				stat_obfIf + " senseless conditions, " +
				stat_obfClasses + " obfuscator classes.");
		}
	}
}

