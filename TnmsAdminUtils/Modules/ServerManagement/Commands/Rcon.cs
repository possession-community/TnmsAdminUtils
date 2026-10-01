using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using TnmsPluginFoundation.Extensions.Client;
using TnmsPluginFoundation.Models.Command;
using TnmsPluginFoundation.Models.Command.Validators;
using TnmsPluginFoundation.Utils.Entity;
using TnmsAdminUtils.Modules.UiInteractions;

namespace TnmsAdminUtils.Modules.ServerManagement.Commands;

public class Rcon(IServiceProvider provider): TnmsAbstractCommandBase(provider)
{
    public override string CommandName => "rcon";
    public override string CommandDescription => "Executes a specified command in server console.";

    private const string Permission = "tnms.adminutil.management.server.command.rcon";

    public override TnmsCommandRegistrationType CommandRegistrationType =>
        TnmsCommandRegistrationType.Client;

    protected override void OnRegistered()
        => ((TnmsAdminUtils)Plugin).AdminMenu.Registry.Register(AdminMenuEntry.Create(CommandName, Permission).Text("AdminMenu.Step.RconCommand"));

    protected override ICommandValidator? GetValidator() => new CompositeValidator()
        .Add(new PermissionValidator(Permission, true))
        .Add(new ArgumentCountValidator(1, true));

    protected override ValidationFailureResult OnValidationFailed(ValidationFailureContext context)
    {
        switch (context.Validator)
        {
            case ArgumentCountValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Rcon.Notification.Usage"));
                break;
        }
        
        return ValidationFailureResult.SilentAbort();
    }

    protected override void ExecuteCommand(IGameClient? client, StringCommand commandInfo, ValidatedArguments? validatedArguments)
    {
        if (client == null)
            return;
        
        string commandToExecute = commandInfo.ArgString;
        
        Plugin.SharedSystem.GetModSharp().ServerCommand(commandToExecute);
        
        string executor = PlayerUtil.GetPlayerName(client);
        Plugin.TnmsLogger.LogAdminAction(client, $"Admin {executor} is executed '{commandToExecute}' with RCON");
        
        foreach (var gameClient in SharedSystem.GetModSharp().GetIServer().GetGameClients(true, true))
        {
            if (gameClient.IsFakeClient || gameClient.IsHltv)
                continue;
            
            gameClient.GetPlayerController()?
                .PrintToChat(
                    ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(gameClient, "Rcon.Broadcast.CommandExecuted", executor, commandToExecute));
        }
    }
}