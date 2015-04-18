using System;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace ManualDeobfuscator
{
	public class ManualPatches : PatchHelpers
	{
		public static void applyManualPatches (ModuleDefinition mainModule)
		{
			OnElement ("StaticDirectories", mainModule.Types, type => {
				return Find ("StaticDirectories", type.Methods, method => {
					return !method.IsConstructor && method.IsPublic && method.IsStatic && method.Parameters.Count == 0 && HasType (method.ReturnType, "System.Boolean") && method.Name.Equals ("CheckIfStartedAsDedicatedServer");
				}
				) != null;
			}, RenameAction<TypeDefinition> ("StaticDirectories"));


			OnElement ("DamageSource", mainModule.GetType ("EntityPlayer").BaseType.Resolve ().Methods,
			    method => !method.IsConstructor && method.IsPublic && method.Parameters.Count == 3 && method.Name.Equals ("DamageEntity"),
				method => {
				RenameAction<TypeDefinition> ("DamageSource") (method.Parameters [0].ParameterType.Resolve ());
				return true;
			}
			);

			OnElement ("EntityPlayer.base.base.getPosition()", mainModule.GetType ("EntityPlayer").BaseType.Resolve ().BaseType.Resolve ().Methods,
			          method => !method.IsConstructor && method.IsPublic && method.Parameters.Count == 0 && method.Name.Equals ("GetPosition"),
			          method => {
				MethodBody body = method.Body;
				body.SimplifyMacros ();
				for (int i = 0; i < body.Instructions.Count; i++) {
					Instruction curInstr = body.Instructions [i];
					if (curInstr.OpCode == OpCodes.Ldfld) {
						if (curInstr.Operand is FieldDefinition) {
							RenameAction<FieldDefinition> ("position") ((FieldDefinition)curInstr.Operand);
						} else
							logger.Error ("A Ldfld instruction has no FieldDefinition");
					}
				}

				body.OptimizeMacros ();
				return true;
			}
			);

			OnElement ("ConnectionManager.DisconnectClient()", mainModule.GetType ("ConnectionManager").Methods,
			          method => !method.IsConstructor && method.IsPublic && method.Parameters.Count == 2 &&
				HasType (method.Parameters [0].ParameterType, "ClientInfo") && 
				HasType (method.Parameters [1].ParameterType, "System.Boolean") &&
				HasType (method.ReturnType, "System.Void"),
			          RenameAction<MethodDefinition> ("DisconnectClient"));

			RenameAction<TypeDefinition> ("ItemBase") (mainModule.GetType ("ItemClassBlock").BaseType.Resolve ());


			OnElement ("PersistentPlayerList.positionToLPBlockOwner", mainModule.GetType ("PersistentPlayerList").Fields,
			          field => HasType (field.FieldType, "System.Collections.Generic.Dictionary") && HasGenericParams (field.FieldType, "Vector3i", "PersistentPlayerData"),
			          MakeFieldPublicAction, RenameAction<FieldDefinition> ("positionToLPBlockOwner"));
				
			OnElement ("PlayerDataFile.inventory", mainModule.GetType ("PlayerDataFile").Fields,
			          field => field.Name.Equals ("inventory") && field.FieldType.IsArray,
			          field => {
				TypeDefinition invType = field.FieldType.Resolve ();
				RenameAction<TypeDefinition> ("InventoryField") (invType);
				OnElement ("InventoryField.count", invType.Fields,
				          field2 => HasType (field2.FieldType, "System.Int32"),
				          RenameAction<FieldDefinition> ("count"));
				OnElement ("InventoryField.itemValue", invType.Fields,
				          field2 => HasType (field2.FieldType, "ItemValue"),
				          RenameAction<FieldDefinition> ("itemValue"));
				return true;
			}
			);


			OnElement ("World.gameTime", mainModule.GetType ("World").Fields,
			          field => HasType (field.FieldType, "System.UInt64"),
			          MakeFieldPublicAction, RenameAction<FieldDefinition> ("gameTime"));


			OnElement ("World.LandClaimIsActive()", mainModule.GetType ("World").Methods,
			          method => !method.IsConstructor && method.IsPrivate && method.Parameters.Count == 1 && HasType (method.Parameters [0].ParameterType, "PersistentPlayerData") && HasType (method.ReturnType, "System.Boolean"),
			          MakeMethodPublicAction, RenameAction<MethodDefinition> ("LandClaimIsActive"));


			OnElement ("World.LandClaimPower()", mainModule.GetType ("World").Methods,
			          method => !method.IsConstructor && method.IsPrivate && method.Parameters.Count == 1 && HasType (method.Parameters [0].ParameterType, "PersistentPlayerData") && HasType (method.ReturnType, "System.Single"),
			          MakeMethodPublicAction, RenameAction<MethodDefinition> ("LandClaimPower"));


			OnElement ("ConnectionManager.connectedClients", mainModule.GetType ("ConnectionManager").Fields,
			          field => HasType (field.FieldType, "DictionarySave") && HasGenericParams (field.FieldType, "System.Int32", "ClientInfo"),
			          MakeFieldPublicAction, RenameAction<FieldDefinition> ("connectedClients"));

		}

	}
}

