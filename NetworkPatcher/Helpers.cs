using System;
using Mono.Cecil;
using System.Collections.Generic;

namespace NetworkPatcher
{
	public class Helpers
	{
		public static Boolean paramlistEquals(Mono.Collections.Generic.Collection<ParameterDefinition> _pars1,
			Mono.Collections.Generic.Collection<ParameterDefinition> _pars2)
		{
			if ((_pars1.Count != _pars2.Count) || _pars1.Count < 0)
				return false;
			ParameterDefinition[] pars1 = _pars1.ToArray();
			ParameterDefinition[] pars2 = _pars2.ToArray();
			for (int i = 0; i < _pars1.Count; i++)
			{
				ParameterDefinition par1 = pars1[i];
				ParameterDefinition par2 = pars2[i];
				if (par1 == null)
				{
					if (par2 != null)
						return false;
				}
				else if (par2 == null)
				{
					if (par1 != null)
						return false;
				}
				else if (!par1.ParameterType.Resolve().Equals(par2.ParameterType.Resolve()))
					return false;
			}
			return true;
		}

		private static void PatchSubMethods(MethodDefinition method, string newName)
		{
			TypeDefinition type = method.DeclaringType;
			ModuleDefinition module = type.Module;
			foreach (TypeDefinition tdef in module.GetTypes())
			{
				if (tdef == type)
					continue;
				TypeDefinition baseType = tdef; TypeReference tmp;
				while ((tmp = baseType.BaseType) != null)
				{
					baseType = tmp.Resolve();
					if (baseType == type)
						break;
				}
				if (baseType != type)
					continue;
				foreach (MethodDefinition mdef in tdef.Methods)
				{
					if (mdef.IsVirtual && mdef.Name.Equals(method.Name) && paramlistEquals(method.Parameters, mdef.Parameters))
					{
						mdef.Name = newName;
						break;
					}
				}
			}
			method.Name = newName;
		}
		public static void PatchVMethod(MethodDefinition method, string newName)
		{
			if (method.IsVirtual)
			{
				TypeDefinition type = method.DeclaringType;
				ModuleDefinition module = type.Module;

				TypeDefinition baseType = type; TypeReference tmp;
				while ((tmp = baseType.BaseType) != null)
				{
					baseType = tmp.Resolve();
					foreach (MethodDefinition mdef in baseType.Methods)
					{
						if (mdef.IsVirtual && mdef.Name.Equals(method.Name) && paramlistEquals(method.Parameters, mdef.Parameters))
						{
							method = mdef;
							break;
						}
					}
				}
				PatchSubMethods(method, newName);
			}
			else
				method.Name = newName;
		}


		public static bool isObfuscated(String name)
		{
			if (name == null || name.Length == 0)
				return true;
			if (name[0] >= '0' && name[0] <= '9')
				return true;
			if (name.StartsWith("cl") || name.StartsWith("scl") || name.StartsWith("mdv") || 
				name.StartsWith("md") || name.StartsWith("fd")  || name.StartsWith("prop"))
				return true;
			bool ret = (name.Length == 5);
			foreach (char ch in name)
			{
				if (
					(
						((ch & 0x00FF) > 0x7F) || (((ch & 0xFF00) >> 8) > 0x7F)
					) ||
					(("" + ch).Normalize().ToCharArray()[0] > 0x00FF) ||
					(((("" + ch).Normalize().ToCharArray()[0] & 0x00FF)) <= 0x20)
				)
				{
					return true;
				}
				if (!((ch >= '0' && ch <= '9') || (ch >= 'A' && ch <= 'Z')))
				{
					ret = false;
				}
			}
			return ret;
		}
		/*public static bool isObfuscated(String name)
		{
			if (name == null)
				return true;
			foreach (char ch in name)
			{
				if (
					(
						((ch & 0x00FF) > 0x7F) || (((ch & 0xFF00) >> 8) > 0x7F)
					) ||
					(("" + ch).Normalize().ToCharArray()[0] > 0x00FF) ||
					(((("" + ch).Normalize().ToCharArray()[0] & 0x00FF)) <= 0x20)
				)
				{
					return true;
				}
			}
			if (name.StartsWith("cl") || name.StartsWith("scl") || name.StartsWith("mdv") || 
				name.StartsWith("md") || name.StartsWith("fd")  || name.StartsWith("prop"))
				return true;
			return false;
		}*/
	}
}

