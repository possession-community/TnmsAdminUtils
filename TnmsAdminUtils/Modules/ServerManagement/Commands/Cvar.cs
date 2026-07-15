using Sharp.Shared.Enums;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using TnmsPluginFoundation.Extensions;
using TnmsPluginFoundation.Extensions.Client;
using TnmsPluginFoundation.Models.Command;
using TnmsPluginFoundation.Models.Command.Validators;
using TnmsPluginFoundation.Utils.Entity;

namespace TnmsAdminUtils.Modules.ServerManagement.Commands;

public class Cvar(IServiceProvider provider): TnmsAbstractCommandBase(provider)
{
    public override string CommandName => "cvar";
    public override string CommandDescription => "Executes a specified command in server console.";

    public override TnmsCommandRegistrationType CommandRegistrationType =>
        TnmsCommandRegistrationType.Client | TnmsCommandRegistrationType.Server;

    protected override ICommandValidator? GetValidator() => new CompositeValidator()
        .Add(new PermissionValidator("tnms.adminutil.management.server.command.cvar", true))
        .Add(new ArgumentCountValidator(1, true));

    protected override ValidationFailureResult OnValidationFailed(ValidationFailureContext context)
    {
        switch (context.Validator)
        {
            case ArgumentCountValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Cvar.Notification.Usage"));
                break;
        }
        
        return ValidationFailureResult.SilentAbort();
    }

    protected override void ExecuteCommand(IGameClient? client, StringCommand commandInfo, ValidatedArguments? validatedArguments)
    {
        var cvar = SharedSystem.GetConVarManager().FindConVar(commandInfo.GetArg(1));
        
        if (cvar == null)
        {
            PrintMessageToServerOrPlayerChat(client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(client, "Cvar.Notification.CvarNotFound", commandInfo.GetArg(1)));
            return;
        }

        if (commandInfo.ArgCount <= 1)
        {
            PrintMessageToServerOrPlayerChat(client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(client, "Cvar.Notification.CurrentCvarValueAndType", commandInfo.GetArg(1), cvar.GetCvarValueString(), cvar.Type.ToString()));
            return;
        }
        
        string value = commandInfo.GetArg(2);

        if (cvar.Flags.HasFlag(ConVarFlags.Cheat))
        {
            cvar.Flags &= ~ConVarFlags.Cheat;
            cvar.SetString(value);
            cvar.Flags |= ConVarFlags.Cheat;
        }
        else
        {
            cvar.SetString(value);
        }
        
        string executor = PlayerUtil.GetPlayerName(client);
        Plugin.TnmsLogger.LogAdminAction(client, $"Admin {executor} changed cvar '{cvar.Name}' to {value}");
        
        foreach (var gameClient in SharedSystem.GetModSharp().GetIServer().GetGameClients(true, true))
        {
            if (gameClient.IsFakeClient || gameClient.IsHltv)
                continue;
            
            gameClient.GetPlayerController()?
                .PrintToChat(
                    ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(gameClient, "Cvar.Broadcast.CvarSet", executor, cvar.Name, value));
        }
    }

}