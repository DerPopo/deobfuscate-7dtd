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

<<<<<<< HEAD

			// Console and ConsoleCommand
			{
				TypeDefinition typeConsole = null;
				TypeDefinition typeSdtdConsole = mainModule.GetType ("SdtdConsole");
				TypeDefinition typeConsoleCommand = null;
				TypeDefinition typeQueuedCommand = null;

				OnElement ("NetTelnetServer.SetConsole()", mainModule.GetType ("NetTelnetServer").Methods,
			          		method => !method.IsConstructor && method.Name.Equals ("SetConsole") && method.IsPublic && method.Parameters.Count == 1 && HasType (method.ReturnType, "System.Void"),
					           method => {
					typeConsole = method.Parameters [0].ParameterType.Resolve ();
					return true; }
				);

				// Console
				if (typeConsole != null) {
					RenameAction<TypeDefinition> ("ConsoleSdtd") (typeConsole);

					OnElement ("NetTelnetServer.console", mainModule.GetType ("NetTelnetServer").Fields,
			          		field => HasType (field.FieldType, typeConsole.Name),
			          		RenameAction<FieldDefinition> ("console"));

					OnElement ("ConsoleSdtd.SendResult()", typeConsole.Methods,
			          		method => !method.IsConstructor && method.IsPublic && method.Parameters.Count == 1 &&
						HasType (method.Parameters [0].ParameterType, "System.String") &&
						method.Parameters [0].Name.Equals ("_line") &&
						HasType (method.ReturnType, "System.Void"),
						RenameAction<MethodDefinition> ("SendResult")
					);

					OnElement ("SdtdConsole.executeCommand()", typeSdtdConsole.Methods,
						method => !method.IsConstructor && !method.IsPublic && method.Parameters.Count == 2 &&
						method.Name.Equals ("executeCommand"),
						MakeMethodPublicAction
					);
					OnElement ("SdtdConsole.commands", typeSdtdConsole.Fields,
						field => HasType (field.FieldType, "System.Collections.Generic.SortedList") && HasGenericParams (field.FieldType, "System.String", "IConsoleCommand"),
						MakeFieldPublicAction, RenameAction<FieldDefinition> ("commands"));
					OnElement ("SdtdConsole.servers", typeSdtdConsole.Fields,
						field => HasType (field.FieldType, "System.Collections.Generic.List") && HasGenericParams (field.FieldType, "IConsoleServer"),
						MakeFieldPublicAction, RenameAction<FieldDefinition> ("servers"));

					OnElement ("SdtdConsole::QueuedCommand", typeSdtdConsole.NestedTypes,
						type => {
							if (Find("SdtdConsole::QueuedCommand.command", type.Fields, field => field.Name.Equals("command")) != null &&
								Find("SdtdConsole::QueuedCommand.sender", type.Fields, field => field.Name.Equals("sender")) != null)
							{
								typeQueuedCommand = type;
								return true;
							}
							return false;
						},
						MakeTypePublicAction, RenameAction<TypeDefinition> ("QueuedCommand"));
					if (typeQueuedCommand != null)
					{
						OnElement ("SdtdConsole.asyncCommands", typeSdtdConsole.Fields,
							field => HasType (field.FieldType, "System.Collections.Generic.List") && HasGenericParams (field.FieldType, typeQueuedCommand),
							MakeFieldPublicAction, RenameAction<FieldDefinition> ("asyncCommands"));
					}
				}
				// END Console
			}
			// END Console and ConsoleCommand

		}

		private static void PatchConsoleCommandMethods (TypeDefinition td, TypeDefinition consoleType, string typeName)
		{
			OnElement (typeName + ".Help()", td.Methods,
			          		method => !method.IsConstructor && method.IsPublic && method.Parameters.Count == 1 &&
				HasType (method.Parameters [0].ParameterType, consoleType.Name) &&
				HasType (method.ReturnType, "System.Void"),
						RenameAction<MethodDefinition> ("Help")
			);

			OnElement (typeName + ".RepeatInterval()", td.Methods,
			          		method => !method.IsConstructor && method.IsPublic && method.Parameters.Count == 0 &&
				HasType (method.ReturnType, "System.Int32"),
						RenameAction<MethodDefinition> ("RepeatInterval")
			);

			OnElement (typeName + ".Names()", td.Methods,
			          		method => !method.IsConstructor && method.IsPublic && method.Parameters.Count == 0 &&
				HasType (method.ReturnType, "System.String[]"),
						RenameAction<MethodDefinition> ("Names")
			);

			OnElement (typeName + ".Run()", td.Methods,
			          		method => !method.IsConstructor && method.IsPublic && method.Parameters.Count == 1 &&
				HasType (method.Parameters [0].ParameterType, "System.String[]") &&
				HasType (method.ReturnType, "System.Void"),
						RenameAction<MethodDefinition> ("Run")
			);

			OnElement (typeName + ".Description()", td.Methods,
			          		method => !method.IsConstructor && method.IsPublic && method.Parameters.Count == 0 &&
				HasType (method.ReturnType, "System.String"),
						RenameAction<MethodDefinition> ("Description")
			);

=======
>>>>>>> e6b6e2433c2347b5a301839b828aeaf69e6927c9
		}

	}
}

