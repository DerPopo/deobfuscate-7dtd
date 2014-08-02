using System;
using Mono.Cecil;
using Mono.Collections.Generic;
using System.Collections.Generic;

namespace ManualDeobfuscator
{
	public class PatchHelpers
	{
		public static int errors = 0;
		public static int success = 0;

		public static DeobfuscateMain.Logger logger = null;

		public delegate bool MatchField (FieldDefinition fd);

		public delegate bool MatchMethod (MethodDefinition md);

		public delegate bool MatchType (TypeDefinition td);

		public delegate bool Match<T> (T e);

		public static bool HasType (TypeReference tr, string typeName)
		{
			string ns = string.Empty;
			if (typeName.Contains (".")) {
				ns = typeName.Substring (0, typeName.LastIndexOf ('.'));
				typeName = typeName.Substring (typeName.LastIndexOf ('.') + 1);
			}

			string trName = tr.Name;
			if (tr.IsGenericInstance) {
				trName = trName.Substring (0, trName.IndexOf ('`'));
			}

			return tr.Namespace.Equals (ns) && trName.Equals (typeName);
		}

		public static bool HasGenericParams (TypeReference tr, params string[] paramNames)
		{
			if (!tr.IsGenericInstance)
				return false;

			GenericInstanceType genType = (GenericInstanceType)tr;
			if (genType.GenericArguments.Count != paramNames.Length)
				return false;

			for (int i = 0; i < paramNames.Length; i++) {
				TypeReference genRef = genType.GenericArguments [i];
				if (!HasType (genRef, paramNames [i]))
					return false;
			}
			return true;
		}



		public delegate bool Action<T> (T def) where T : class, IMemberDefinition;

		public static Action<T> RenameAction<T> (string newName) where T : class, IMemberDefinition
		{
			return def => {
				setName (def, newName);
				return true; };
		}

		public static Action<FieldDefinition> MakeFieldPublicAction = def => {
				def.Attributes = def.Attributes & (~Mono.Cecil.FieldAttributes.Private) | Mono.Cecil.FieldAttributes.Public;
				return true;
			};
		public static Action<MethodDefinition> MakeMethodPublicAction = def => {
				def.IsPrivate = false;
				def.IsPublic = true;
				return true;
			};

		public static T Find<T> (string description, Collection<T> container, Match<T> matcher) where T : class, IMemberDefinition
		{
			T found = null;
			if (container != null) {
				foreach (T elem in container) {
					if (matcher (elem)) {
						if (found != null) {
							errors++;
							Log ("ERROR (" + description + "): Multiple matching " + typeof(T).Name + "s found!");
							return null;
						}
						found = elem;
					}
				}
			}

			return found;
		}

		public static void OnElement <T> (string description, Collection<T> container, Match<T> matcher, params Action<T>[] actions) where T : class, IMemberDefinition
		{
			T found = null;
			if (container != null) {
				foreach (T elem in container) {
					if (matcher (elem)) {
						if (found != null) {
							errors++;
							Log ("ERROR (" + description + "): Multiple matching " + typeof(T).Name + "s found!");
							return;
						}
						found = elem;
					}
				}
			}

			if (found == null) {
				errors++;
				Log ("ERROR (" + description + "): " + typeof(T).Name + " not found!");
				return;
			}

			foreach (Action<T> a in actions) {
				if (!a (found)) {
					errors++;
					Log ("ERROR (" + description + "): Applying action failed!");
					return;
				}
			}

			success++;
		}



		public static Dictionary<IMemberDefinition, string> clnamestomod = new Dictionary<IMemberDefinition, string> ();

		public static void setName (IMemberDefinition def, string name)
		{
			foreach (KeyValuePair<IMemberDefinition, string> entry in clnamestomod) {
				if (def.Equals (entry.Key)) {
					clnamestomod.Remove (def);
					break;
				}
			}
			clnamestomod.Add (def, name);
		}

		public static void FinalizeNormalizing ()
		{
			foreach (KeyValuePair<IMemberDefinition, string> vce in clnamestomod) {
				try {
					vce.Key.Name = vce.Value;
				} catch (Exception e) {
					Log ("An exception occured : ");
					Log (e.ToString ());
				}
			}
		}


		public static void Log(string message = "") {
			if (logger != null)
				logger.Log(message);
			else
				Console.WriteLine(message);
		}

	}
}

