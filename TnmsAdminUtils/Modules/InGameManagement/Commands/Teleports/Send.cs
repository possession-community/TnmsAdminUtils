using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using TnmsAdminUtils.Utils;
using TnmsPluginFoundation.Extensions.Client;
using TnmsPluginFoundation.Models.Command;
using TnmsPluginFoundation.Models.Command.Validators;
using TnmsPluginFoundation.Utils.Entity;

namespace TnmsAdminUtils.Modules.InGameManagement.Commands.Teleports;

public class Send(IServiceProvider provider): TnmsAbstractCommandBase(provider)
{
    public override string CommandName => "send";
    public override string CommandDescription => "send a player to target.";

    public override TnmsCommandRegistrationType CommandRegistrationType =>
        TnmsCommandRegistrationType.Client | TnmsCommandRegistrationType.Server;

    protected override ICommandValidator? GetValidator() => new CompositeValidator()
        .Add(new PermissionValidator("tnms.adminutil.management.ingame.command.send", true))
        .Add(new ArgumentCountValidator(2, true))
        .Add(new TargetValidator(1, true))
        .Add(new TargetValidator(2, true));

    protected override ValidationFailureResult OnValidationFailed(ValidationFailureContext context)
    {
        switch (context.Validator)
        {
            case ArgumentCountValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Teleport.Notification.Send.Usage"));
                break;
            case PermissionValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Common.ValidationFailure.NotEnoughPermissions"));
                break;
            case TargetValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Common.ValidationFailure.NoValidTargetsFound"));
                break;
        }
        
        return ValidationFailureResult.SilentAbort();
    }

    protected override void ExecuteCommand(IGameClient? client, StringCommand commandInfo, ValidatedArguments? validatedArguments)
    {
        var targets = validatedArguments!.GetArgument<List<IGameClient>>(1)!;
        var sendTarget = validatedArguments!.GetArgument<List<IGameClient>>(2)!;

        if (sendTarget.Count != 1)
        {
            PrintMessageToServerOrPlayerChat(client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(client, "Common.ValidationFailure.MultipleTargetsFound"));
            return;
        }
        
        var sendTargetPawn = sendTarget[0].GetPlayerController()?.GetPlayerPawn();

        if (sendTargetPawn == null)
        {
            PrintMessageToServerOrPlayerChat(client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(client, "Common.ValidationFailure.NoValidTargetsFound"));
            return;
        }
        
        foreach (var gameClient in targets)
        {
            var pawn = gameClient.GetPlayerController()?.GetPlayerPawn();
            
            if (pawn == null)
                continue;

            pawn.Teleport(sendTargetPawn.GetAbsOrigin());
        }

        string targetName = targets.GetTargetName();
        string sendTargetName = sendTargetPawn.GetController()?.PlayerName ?? "N/A";
        string executor = PlayerUtil.GetPlayerName(client);
        Plugin.TnmsLogger.LogAdminAction(client, $"Admin {executor} sent {targetName} to {sendTargetName}");
    
        foreach (var gameClient in SharedSystem.GetModSharp().GetIServer().GetGameClients())
        {
            if (gameClient.IsFakeClient || gameClient.IsHltv)
                continue;
        
            gameClient.GetPlayerController()?
                .PrintToChat(
                    ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(gameClient, "Teleport.Broadcast.Send", executor, targets.GetTargetName(), sendTargetName));
        }
    }
}