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

			//-----------------------------TileEntities-----------------------------
			TileEntityPatcher.Patch(logger, asmCSharp);
			//--------------------------AuthenticatePlayer--------------------------

			HelperClass.executeActions<MethodDefinition>(module, "GameManager", new HelperClass.GenericFuncContainer<MethodDefinition, bool>[]{
				HelperClass.MethodParametersComparer("ClientInfo", "System.String", "EnumAuthenticationResult"),
				HelperClass.MethodReturnTypeComparer("System.Void"),
				/*HelperClass.MethodOPCodeComparer(new int[]{-13,-10,-6,-5,-4,-3,-2},//new int[]{1,4,8,9,10,11,12}, 
					new OpCode[]{OpCodes.Ldsfld,OpCodes.Newarr,OpCodes.Stelem_Ref,OpCodes.Callvirt,OpCodes.Ldarg_2,OpCodes.Ldc_I4_1,OpCodes.Call},
					null)*/
			}, HelperClass.MemberNameSetter<MethodDefinition>("DenyPlayer"));
		}
	}
}

