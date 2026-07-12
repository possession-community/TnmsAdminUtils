using Sharp.Shared.Enums;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using TnmsAdminUtils.Utils;
using TnmsPluginFoundation.Extensions.Client;
using TnmsPluginFoundation.Models.Command;
using TnmsPluginFoundation.Models.Command.Validators;
using TnmsPluginFoundation.Utils.Entity;

namespace TnmsAdminUtils.Modules.InGameManagement.Commands;

public class SwapTeam(IServiceProvider provider) : TnmsAbstractCommandBase(provider)
{
    public override string CommandName => "swap";
    public override string CommandDescription => "Swaps a player to the opposite team without killing them.";

    public override TnmsCommandRegistrationType CommandRegistrationType =>
        TnmsCommandRegistrationType.Client | TnmsCommandRegistrationType.Server;

    protected override ICommandValidator? GetValidator() => new CompositeValidator()
        .Add(new PermissionValidator("tnms.adminutil.management.ingame.command.team", true))
        .Add(new ArgumentCountValidator(1, true))
        .Add(new TargetValidator(1, true));

    protected override ValidationFailureResult OnValidationFailed(ValidationFailureContext context)
    {
        switch (context.Validator)
        {
            case ArgumentCountValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Team.Notification.SwapUsage"));
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

        foreach (var gameClient in targets)
        {
            var pawn = gameClient.GetPlayerPawn();
            if (pawn == null)
                continue;

            var currentTeam = pawn.Team;
            CStrikeTeam newTeam = currentTeam == CStrikeTeam.TE ? CStrikeTeam.CT : CStrikeTeam.TE;

            var controller = gameClient.GetPlayerController();
            controller?.SwitchTeam(newTeam);
        }

        string executor = PlayerUtil.GetPlayerName(client);
        string targetName = targets.GetTargetName();
        Plugin.TnmsLogger.LogAdminAction(client, $"Admin {executor} swapped {targetName} team");

        foreach (var gameClient in SharedSystem.GetModSharp().GetIServer().GetGameClients())
        {
            if (gameClient.IsFakeClient || gameClient.IsHltv)
                continue;

            gameClient.GetPlayerController()?
                .PrintToChat(
                    ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(gameClient, "Team.Broadcast.TeamSwapped", executor, targetName));
        }
    }
}
