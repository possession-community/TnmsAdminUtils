using Microsoft.Extensions.Logging;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using TnmsExtendableTargeting.Shared;
using TnmsPluginFoundation.Extensions.Client;
using TnmsPluginFoundation.Models.Command;
using TnmsPluginFoundation.Models.Command.Validators;
using TnmsPluginFoundation.Utils.Entity;

namespace TnmsAdminUtils.Modules.ClientManagement.Commands;

public class ReplicateCvar(IServiceProvider provider): TnmsAbstractCommandBase(provider)
{
    public override string CommandName => "rcvar";
    public override string CommandDescription => "Replicate cvar value to the client.";
    
    public override TnmsCommandRegistrationType CommandRegistrationType =>
        TnmsCommandRegistrationType.Client;

    protected override ICommandValidator? GetValidator() => new CompositeValidator()
        .Add(new PermissionValidator("tnms.adminutil.management.server.command.rcvar", true))
        .Add(new ArgumentCountValidator(3, true))
        .Add(new ExtendableTargetValidator(1, true, true));

    protected override ValidationFailureResult OnValidationFailed(ValidationFailureContext context)
    {
        switch (context.Validator)
        {
            case PermissionValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Common.ValidationFailure.NotEnoughPermissions"));
                break;
            case ArgumentCountValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "ReplicateCvar.Notification.Usage"));
                break;
            case ExtendableTargetValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Common.ValidationFailure.NoValidTargetsFound"));
                break;
        }
        return ValidationFailureResult.SilentAbort();
    }

    protected override void ExecuteCommand(IGameClient? client, StringCommand commandInfo, ValidatedArguments? validatedArguments)
    {
        if (client == null)
            return;
        
        var cvar = SharedSystem.GetConVarManager().FindConVar(commandInfo.GetArg(2));
        
        if (cvar == null)
        {
            PrintMessageToServerOrPlayerChat(client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(client, "Cvar.Notification.CvarNotFound", commandInfo.GetArg(2)));
            return;
        }
        
        var targets = validatedArguments!.GetArgument<ITargetingResult>(1)!;
        
        string value = commandInfo.GetArg(3);

        bool conVarHasReplicateFlag = cvar.Flags.HasFlag(Sharp.Shared.Enums.ConVarFlags.Replicated);

        try
        {
            if (!conVarHasReplicateFlag)
                cvar.Flags |= Sharp.Shared.Enums.ConVarFlags.Replicated;

            foreach (var target in targets.GetTargets())
            {
                if (target.IsFakeClient || target.IsHltv)
                    continue;

                cvar.ReplicateToClient(target, value);
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Exception while replicate cvar");
        }
        finally
        {
            if (!conVarHasReplicateFlag) 
                cvar.Flags &= ~Sharp.Shared.Enums.ConVarFlags.Replicated;
        }
        
        Plugin.TnmsLogger.LogAdminAction(client, $"Admin {PlayerUtil.GetPlayerName(client)} replicated ConVar to {targets.GetTargetName()} | ConVar: {cvar.Name}, value: {value}");
        
        client.GetPlayerController()?.PrintToChat(((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(client, "ReplicateCvar.Notification.Replicated", cvar.Name, value, targets.GetTargetName(Plugin.Localizer.GetClientCulture(client))));
    }
}