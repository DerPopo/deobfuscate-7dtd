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
			OnElement ("ConnectionManager.connectedClients", mainModule.GetType ("ConnectionManager").Fields,
			          field => HasType (field.FieldType, "ClientInfoCollection"),
			          MakeFieldPublicAction, RenameAction<FieldDefinition> ("connectedClients"));

			OnElement ("ClientInfoCollection.clientsByEntityId", mainModule.GetType ("ClientInfoCollection").Fields,
				field => HasType (field.FieldType, "System.Collections.Generic.Dictionary") && HasGenericParams (field.FieldType, "System.Int32", "ClientInfo"),
				MakeFieldPublicAction, RenameAction<FieldDefinition> ("clientsByEntityId"));

			OnElement ("ClientInfoCollection.clientsBySteamId", mainModule.GetType ("ClientInfoCollection").Fields,
				field => HasType (field.FieldType, "System.Collections.Generic.Dictionary") && HasGenericParams (field.FieldType, "Steamworks.CSteamID", "ClientInfo"),
				MakeFieldPublicAction, RenameAction<FieldDefinition> ("clientsBySteamId"));

			OnElement ("ClientInfoCollection.clientsByNetworkPlayer", mainModule.GetType ("ClientInfoCollection").Fields,
				field => HasType (field.FieldType, "System.Collections.Generic.Dictionary") && HasGenericParams (field.FieldType, "UnityEngine.NetworkPlayer", "ClientInfo"),
				MakeFieldPublicAction, RenameAction<FieldDefinition> ("clientsByNetworkPlayer"));

			OnElement ("ClientInfoCollection.clientsByPlayerId", mainModule.GetType ("ClientInfoCollection").Fields,
				field => HasType (field.FieldType, "System.Collections.Generic.Dictionary") && HasGenericParams (field.FieldType, "System.String", "ClientInfo"),
				MakeFieldPublicAction, RenameAction<FieldDefinition> ("clientsByPlayerId"));

			OnElement ("ClientInfoCollection.clients", mainModule.GetType ("ClientInfoCollection").Fields,
				field => HasType (field.FieldType, "System.Collections.Generic.List") && HasGenericParams (field.FieldType, "ClientInfo"),
				MakeFieldPublicAction, RenameAction<FieldDefinition>("clients"));

			// Console and ConsoleCommand
			{
				TypeDefinition typeSdtdConsole = mainModule.GetType ("SdtdConsole");
				TypeDefinition typeQueuedCommand = null;

				// Console
				if (typeSdtdConsole != null) {
					OnElement ("SdtdConsole.executeCommand()", typeSdtdConsole.Methods,
						method => !method.IsConstructor && !method.IsPublic && method.Parameters.Count == 2 &&
						HasType(method.Parameters[0].ParameterType, "System.String") &&
						HasType(method.Parameters[1].ParameterType, "CommandSenderInfo"),
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
							if (Find("SdtdConsole::QueuedCommand.command", type.Fields, field => HasType(field.FieldType, "System.String")) != null &&
								Find("SdtdConsole::QueuedCommand.sender", type.Fields, field => HasType(field.FieldType, "IConsoleConnection")) != null)
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

						OnElement ("SdtdConsole::QueuedCommand.command", typeQueuedCommand.Fields,
							field => HasType (field.FieldType, "System.String"),
							RenameAction<FieldDefinition>("command"));

						OnElement ("SdtdConsole::QueuedCommand.sender", typeQueuedCommand.Fields,
							field => HasType (field.FieldType, "IConsoleConnection"),
							RenameAction<FieldDefinition>("sender"));
					}
				}
				// END Console
			}
			// END Console and ConsoleCommand

		}

	}
}

