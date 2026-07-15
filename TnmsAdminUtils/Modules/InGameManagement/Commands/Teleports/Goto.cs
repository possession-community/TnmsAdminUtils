using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using TnmsPluginFoundation.Extensions.Client;
using TnmsPluginFoundation.Models.Command;
using TnmsPluginFoundation.Models.Command.Validators;
using TnmsPluginFoundation.Utils.Entity;

namespace TnmsAdminUtils.Modules.InGameManagement.Commands.Teleports;

public class Goto(IServiceProvider provider): TnmsAbstractCommandBase(provider)
{
    public override string CommandName => "goto";
    public override string CommandDescription => "Teleport to player.";

    public override TnmsCommandRegistrationType CommandRegistrationType =>
        TnmsCommandRegistrationType.Client;

    protected override ICommandValidator? GetValidator() => new CompositeValidator()
        .Add(new PermissionValidator("tnms.adminutil.management.ingame.command.goto", true))
        .Add(new ArgumentCountValidator(1, true))
        .Add(new TargetValidator(1, true));

    protected override ValidationFailureResult OnValidationFailed(ValidationFailureContext context)
    {
        switch (context.Validator)
        {
            case ArgumentCountValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Teleport.Notification.Goto.Usage"));
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
        var executorPawn = client?.GetPlayerPawn();
        if (executorPawn == null)
            return;
        
        var targets = validatedArguments!.GetArgument<List<IGameClient>>(1)!;

        if (targets.Count != 1)
        {
            PrintMessageToServerOrPlayerChat(client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(client, "Common.ValidationFailure.MultipleTargetsFound"));
            return;
        }
        
        var targetPawn = targets.FirstOrDefault()?.GetPlayerController()?.GetPlayerPawn();
        
        if (targetPawn == null)
        {
            PrintMessageToServerOrPlayerChat(client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(client, "Common.ValidationFailure.NoValidTargetsFound"));
            return;
        }
        
        executorPawn.Teleport(targetPawn.GetAbsOrigin());
        
        string executor = PlayerUtil.GetPlayerName(client);
        string targetName = targetPawn.GetController()?.PlayerName ?? "N/A";
        Plugin.TnmsLogger.LogAdminAction(client, $"Admin {executor} teleported to {targetName}");
    
        foreach (var gameClient in SharedSystem.GetModSharp().GetIServer().GetGameClients(true, true))
        {
            if (gameClient.IsFakeClient || gameClient.IsHltv)
                continue;
        
            gameClient.GetPlayerController()?
                .PrintToChat(
                    ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(gameClient, "Teleport.Broadcast.Goto", executor, targetName));
        }
    }
}