using Sharp.Shared.Enums;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using TnmsAdminUtils.Utils;
using TnmsPluginFoundation.Extensions.Client;
using TnmsPluginFoundation.Models.Command;
using TnmsPluginFoundation.Models.Command.Validators;
using TnmsPluginFoundation.Utils.Entity;
using TnmsAdminUtils.Modules.UiInteractions;

namespace TnmsAdminUtils.Modules.InGameManagement.Commands;

public class NoClip(IServiceProvider provider) : TnmsAbstractCommandBase(provider)
{
    public override string CommandName => "noclip";
    public override string CommandDescription => "Toggles noclip on a player.";

    private const string Permission = "tnms.adminutil.management.ingame.command.noclip";

    public override TnmsCommandRegistrationType CommandRegistrationType =>
        TnmsCommandRegistrationType.Client | TnmsCommandRegistrationType.Server;

    protected override void OnRegistered()
        => ((TnmsAdminUtils)Plugin).AdminMenu.Registry.Register(AdminMenuEntry.Create(CommandName, Permission).Target().Toggle().Optional());

    protected override ICommandValidator? GetValidator() => new CompositeValidator()
        .Add(new PermissionValidator(Permission, true))
        .Add(new ArgumentCountValidator(1, true))
        .Add(new TargetValidator(1, true));

    protected override ValidationFailureResult OnValidationFailed(ValidationFailureContext context)
    {
        switch (context.Validator)
        {
            case ArgumentCountValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "NoClip.Notification.Usage"));
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

        int? explicitValue = null;
        if (commandInfo.ArgCount > 2 && int.TryParse(commandInfo.GetArg(2), out int val))
            explicitValue = Math.Clamp(val, 0, 1);

        foreach (var gameClient in targets)
        {
            var pawn = gameClient.GetPlayerPawn();
            if (pawn == null)
                continue;

            bool enableNoclip = explicitValue.HasValue
                ? explicitValue.Value == 1
                : pawn.MoveType != MoveType.NoClip;

            pawn.SetMoveType(enableNoclip ? MoveType.NoClip : MoveType.Walk);
        }

        string executor = PlayerUtil.GetPlayerName(client);
        string targetName = targets.GetTargetName();
        string state = explicitValue.HasValue ? (explicitValue.Value == 1 ? "ON" : "OFF") : "toggled";
        Plugin.TnmsLogger.LogAdminAction(client, $"Admin {executor} set noclip {state} on {targetName}");

        foreach (var gameClient in SharedSystem.GetModSharp().GetIServer().GetGameClients(true, true))
        {
            if (gameClient.IsFakeClient || gameClient.IsHltv)
                continue;

            gameClient.GetPlayerController()?
                .PrintToChat(
                    ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(gameClient, "NoClip.Broadcast.NoClipSet", executor, targetName, state));
        }
    }
}
