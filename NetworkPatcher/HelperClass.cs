using System;
using DeobfuscateMain;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Collections.Generic;
using System.Reflection;

namespace NetworkPatcher
{
	public class HelperClass
	{
		private static Logger logger = null;
		public static void SetLogger(Logger _logger)
		{
			logger = _logger;
		}

		public static Func<T,bool> MemberNameComparer<T>(string name) where T : IMemberDefinition
		{
			return member => {
				return member.Name.Equals(name);
			};
		}
		protected static string writeGenericArgument(TypeReference tref)
		{
			if (tref.Resolve() == null)
				return "";
			System.Text.StringBuilder retBuilder = new System.Text.StringBuilder();
			retBuilder.Append(tref.Resolve().FullName);
			if (retBuilder.Length > 2 && retBuilder[retBuilder.Length-2] == '\u0060')
				retBuilder.Remove(retBuilder.Length-2,2);
			if (tref is GenericInstanceType && ((GenericInstanceType)tref).GenericArguments.Count > 0)
			{
				GenericInstanceType gitPar = (GenericInstanceType)tref;
				if (gitPar.GenericArguments.Count > 0)
					retBuilder.Append('<');
				foreach (TypeReference _gpar in gitPar.GenericArguments) {
					retBuilder.Append(writeGenericArgument(_gpar));
				}
				if (gitPar.GenericArguments.Count > 0)
					retBuilder[retBuilder.Length-1] = '>';
			}
			return retBuilder.ToString() + ",";
		}
		public static Func<FieldDefinition,bool> FieldTypeComparer(string fieldType)
		{
			return field => {
				TypeReference tref = field.FieldType;
				TypeDefinition type = field.FieldType.Resolve();
				if (type == null)
					return false;
				System.Text.StringBuilder typeNameBuilder = new System.Text.StringBuilder();
				typeNameBuilder.Append(type.FullName);
				if ((type.GenericParameters.Count > 0) && (tref is GenericInstanceType))
				{
					typeNameBuilder.Remove(typeNameBuilder.Length-2,2);
					typeNameBuilder.Append('<');
					foreach (TypeReference garg in ((GenericInstanceType)tref).GenericArguments)
					{
						typeNameBuilder.Append(writeGenericArgument(garg));
					}
					typeNameBuilder[typeNameBuilder.Length-1] = '>';
				}
				return typeNameBuilder.ToString().Equals(fieldType);
			};
		}
		public static Func<FieldDefinition,bool> FieldAttributeComparer(Mono.Cecil.FieldAttributes attrs)
		{
			return field => {
				return (field.Attributes & attrs) == attrs;
			};
		}
		public static Func<FieldDefinition,bool> FieldNegAttributeComparer(Mono.Cecil.FieldAttributes negAttrs)
		{
			return field => {
				return (field.Attributes & negAttrs) == 0;
			};
		}
		public static Func<MethodDefinition,bool> MethodAttributeComparer(Mono.Cecil.MethodAttributes attrs)
		{
			return method => {
				return (method.Attributes & attrs) == attrs;
			};
		}
		public static Func<MethodDefinition,bool> MethodNegAttributeComparer(Mono.Cecil.MethodAttributes negAttrs)
		{
			return method => {
				return (method.Attributes & negAttrs) == 0;
			};
		}
		public static Func<MethodDefinition,bool> MethodReturnTypeComparer(string returnType)
		{
			return method => {
				return method.ReturnType.FullName.Equals(returnType);
			};
		}
		public static Func<MethodDefinition,bool> MethodParametersComparer(params string[] parameterTypes)
		{
			return method => {
				if (method.Parameters.Count != parameterTypes.Length)
					return false;
				for (int i = 0; i < method.Parameters.Count; i++)
				{
					if (!method.Parameters[i].ParameterType.FullName.Equals(parameterTypes[i]))
						return false;
				}
				return true;
			};
		}
		public static Func<MethodDefinition,bool> MethodOPCodeComparer(int[] indices, OpCode[] opCodes, object[] operands)
		{
			if (indices.Length != opCodes.Length || (operands != null && operands.Length != opCodes.Length))
			{
				OnError(ErrorCode.INVALID_PARAMETER, "MethodOPCodeComparer : all arrays should have the same size");
				return null;
			}
			return method => {
				Instruction[] instrs = method.Body.Instructions.ToArray();
				for (int i = 0; i < indices.Length; i++)
				{
					int index = indices[i] < 0 ? (instrs.Length + indices[i]) : indices[i];
					if ((index > instrs.Length) || (index < 0))
						return false;
					if (!HelperClass.OPMatches(instrs[index], opCodes[i], (operands != null) ? operands[i] : null))
						return false;
				}
				return true;
			};
		}

		public static T findMember<T>(ModuleDefinition module, object type, bool allowMultipleResults, params Func<T,bool>[] comparers)
			where T : IMemberDefinition
		{
			T[] ret = findMembers<T>(module, type, comparers);
			List<T> nullContainer = new List<T>();
			//workaround for compiler errors (I assume that nobody tries to create a value type implementing IMemberDefinition..)
			nullContainer.GetType().GetMethod("Add", new Type[]{typeof(T)}).Invoke(nullContainer, new object[]{ null });
			if (ret == null || ret.Length == 0)
			{
				OnError(ErrorCode.MEMBER_NOT_FOUND, typeof(T).Name, Environment.StackTrace);
				return nullContainer[0];
			}
			//if (ret.Length == 0)
			//	return nullContainer[0];
			if (!allowMultipleResults && ret.Length > 1)
			{
				OnError(ErrorCode.MULTIPLE_RESULTS, typeof(T).Name, Environment.StackTrace);
				return nullContainer[0];
			}
			return ret[0];
		}
		public static T[] findMembers<T>(ModuleDefinition module, object type, params Func<T,bool>[] comparers)
			where T : IMemberDefinition
		{
			if (type != null)
			{
				TypeDefinition tdef = (type is string) ? module.GetType((string)type) : ((type is TypeDefinition) ? ((TypeDefinition)type) : null);
				if (tdef == null) {
					OnError (ErrorCode.TYPE_NOT_FOUND, (type is string) ? ((string)type) : "(null)");
					return null;
				}
				T[] memberArray = null;
				object memberCollection = null;
				if (typeof(T) == typeof(MethodDefinition))
					memberCollection = tdef.Methods;
				else if (typeof(T) == typeof(FieldDefinition))
					memberCollection = tdef.Fields;
				else if (typeof(T) == typeof(PropertyDefinition))
					memberCollection = tdef.Properties;
				else if (typeof(T) == typeof(TypeDefinition))
					memberCollection = tdef.NestedTypes;
				if (memberCollection == null)
					throw new NotSupportedException ("member type " + typeof(T).Name);
				//another workaround for compiler errors (when converting to T[])
				memberArray = (T[])memberCollection.GetType().GetMethod("ToArray", new Type[0]).Invoke(memberCollection, new object[0]);
				List<T> members = new List<T>();
				foreach (T member in memberArray)
				{
					bool matches = true;
					foreach (Func<T,bool> comparer in comparers)
					{
						if (comparer == null || !comparer(member)) {
							matches = false;
							break;
						}
					}
					if (matches)
						members.Add(member);
				}
				return members.ToArray();
			}
			else
			{
				List<T> members = new List<T>();
				foreach (TypeDefinition tdef in module.Types)
					members.AddRange(findMembers<T>(module, tdef, comparers));
				return members.ToArray();
			}
		}

		public static Func<MethodDefinition,bool> MethodAttributeSetter(Mono.Cecil.MethodAttributes attrs)
		{
			return method => {
				method.Attributes = (method.Attributes & ~attrs) | attrs;
				return true;
			};
		}
		public static Func<T,bool> MemberNameSetter<T>(string name)
			where T : IMemberDefinition
		{
			return member => {
				member.Name = name;
				return true;
			};
		}
		public static void executeActions<T>(ModuleDefinition module, object type, Func<T,bool>[] comparers, params Func<T,bool>[] actions)
			where T : IMemberDefinition
		{
			T[] members = findMembers<T>(module, type, comparers);
			if (members.Length == 0)
				OnError (ErrorCode.MEMBER_NOT_FOUND, typeof(T).Name, Environment.StackTrace);
			foreach (T member in members)
			{
				foreach (Func<T,bool> action in actions)
				{
					if (!action(member))
						OnError(ErrorCode.ACTION_FAILED, "member " + member.FullName);
				}
				logger.Info("Patched " + member.FullName + ".");
			}
		}
		public static void executeActions(ModuleDefinition module, object type, params Func<TypeDefinition,bool>[] actions)
		{
			TypeDefinition tdef = (type is string) ? module.GetType((string)type) : ((type is TypeDefinition) ? ((TypeDefinition)type) : null);
			if (tdef == null) {
				OnError(ErrorCode.TYPE_NOT_FOUND, (type is string) ? ((string)type) : "(null)");
				return;
			}
			foreach (Func<TypeDefinition,bool> action in actions)
			{
				if (!action(tdef))
					OnError(ErrorCode.ACTION_FAILED, "type " + tdef.FullName);
			}
			logger.Info("Patched " + tdef.FullName + ".");
		}
		public static void executeActions<T>(T type, params Func<T,bool>[] actions)
			where T : IMemberDefinition
		{
			string fullName;
			PropertyInfo fullNameProp = type.GetType().GetProperty("FullName");
			if (fullNameProp != null)
				fullName = (string)fullNameProp.GetGetMethod().Invoke(type, new object[0]);
			else
				fullName = type.ToString();

			foreach (Func<T,bool> action in actions)
			{
				if (!action(type))
					OnError(ErrorCode.ACTION_FAILED, fullName);
			}
			logger.Info("Patched " + type.FullName + ".");
		}

		protected static bool OPMatches(Instruction instr, OpCode op, object operand)
		{
			if (instr.OpCode != op ||
				(
					(operand != null) ? 
					((operand != null && instr.Operand == null) || 
						!operand.Equals(instr.Operand)) 
					: false
				)) 
			{
				return false;
			}
			return true;
		}

		public static int[] FindOPCodePattern(MethodDefinition mdef, OpCode[] pattern, int offset = 0, object[] operands = null)
		{
			if (pattern.Length == 0)
				return new int[0];
			Mono.Collections.Generic.Collection<Instruction> instrs = mdef.Body.Instructions;
			List<int> results = new List<int>();
			for (int i = 0; i < (instrs.Count-pattern.Length); i++)
			{
				bool matches = true;
				for (int _i = 0; _i < pattern.Length; _i++)
				{
					Instruction curInstr = instrs[i+_i];
					if (!OPMatches(curInstr, pattern[_i], (operands == null) ? null : operands[_i])) 
					{
						matches = false;
						break;
					}
				}
				if (matches)
					results.Add(i);
			}
			return results.ToArray();
		}


		class ErrorInfo : Attribute
		{
			public string description;
			public int code;
			public ErrorInfo(string description, int code)
			{
				this.description = description;
				this.code = code;
			}
		}
		private enum ErrorCode
		{
			[ErrorInfo("Unable to find type {0}!", 0)]
			TYPE_NOT_FOUND,
			[ErrorInfo("Multiple results of type {0} found!\r\n{1}", 1)]
			MULTIPLE_RESULTS,
			[ErrorInfo("Unable to find a member of type {0}!\r\n{1}", 2)]
			MEMBER_NOT_FOUND,
			[ErrorInfo("Unable to apply patches to {0}!", 3)]
			ACTION_FAILED,
			[ErrorInfo("Invalid parameter : {0}!", 4)]
			INVALID_PARAMETER,
		};
		private static void OnError(ErrorCode error, params object[] args)
		{
			if (logger == null)
				return;
			MemberInfo[] codeInfo = typeof(ErrorCode).GetMember(error.ToString());
			if (codeInfo == null || codeInfo.Length <= 0)
			{
				logger.Error("Something really bad happened while executing OnError (cannot find " + error.ToString() + " in ErrorCode).");
				return;
			}
			object[] errorInfoAttrs = codeInfo[0].GetCustomAttributes(typeof(ErrorInfo), false);
			if (errorInfoAttrs.Length == 1 && errorInfoAttrs[0] is ErrorInfo) 
			{
				ErrorInfo errInfo = (ErrorInfo)errorInfoAttrs[0];
				logger.Error(String.Format(errInfo.description, args));
			}
			else
				logger.Error("OnError : cannot find the ErrorInfo attribute for " + error.ToString() + ".");
		}
	}
}

