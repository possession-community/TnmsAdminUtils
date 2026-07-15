using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using TnmsAdminUtils.Utils;
using TnmsPluginFoundation.Extensions.Client;
using TnmsPluginFoundation.Models.Command;
using TnmsPluginFoundation.Models.Command.Validators;
using TnmsPluginFoundation.Utils.Entity;

namespace TnmsAdminUtils.Modules.InGameManagement.Commands;

public class Freeze(IServiceProvider provider) : TnmsAbstractCommandBase(provider)
{
    private static readonly Dictionary<int, Guid> FreezeTimers = new();
    private static ISharedSystem? _sharedSystem;

    public override string CommandName => "freeze";
    public override string CommandDescription => "Freezes a player in place.";

    public override TnmsCommandRegistrationType CommandRegistrationType =>
        TnmsCommandRegistrationType.Client | TnmsCommandRegistrationType.Server;

    protected override ICommandValidator? GetValidator() => new CompositeValidator()
        .Add(new PermissionValidator("tnms.adminutil.management.ingame.command.freeze", true))
        .Add(new ArgumentCountValidator(1, true))
        .Add(new TargetValidator(1, true));

    protected override ValidationFailureResult OnValidationFailed(ValidationFailureContext context)
    {
        switch (context.Validator)
        {
            case ArgumentCountValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Freeze.Notification.Usage"));
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
        _sharedSystem = SharedSystem;
        var targets = validatedArguments!.GetArgument<List<IGameClient>>(1)!;

        float duration = -1.0f;
        if (commandInfo.ArgCount > 2 && float.TryParse(commandInfo.GetArg(2), out float parsed) && parsed > 0)
            duration = parsed;

        foreach (var gameClient in targets)
        {
            var pawn = gameClient.GetPlayerPawn();
            if (pawn == null)
                continue;

            CancelFreezeTimer(gameClient.Slot);
            pawn.SetMoveType(MoveType.None);
            pawn.RenderColor = new Color32(0, 255, 0, 255);

            if (duration > 0)
            {
                var timerId = Plugin.CreateTimer(duration, () => UnfreezePlayer(gameClient));
                FreezeTimers[gameClient.Slot] = timerId;
            }
        }

        string executor = PlayerUtil.GetPlayerName(client);
        string targetName = targets.GetTargetName();
        Plugin.TnmsLogger.LogAdminAction(client, $"Admin {executor} froze {targetName}");

        foreach (var gameClient in SharedSystem.GetModSharp().GetIServer().GetGameClients(true, true))
        {
            if (gameClient.IsFakeClient || gameClient.IsHltv)
                continue;

            gameClient.GetPlayerController()?
                .PrintToChat(
                    ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(gameClient, "Freeze.Broadcast.Frozen", executor, targetName));
        }
    }

    internal static void UnfreezePlayer(IGameClient gameClient)
    {
        var pawn = gameClient.GetPlayerPawn();
        if (pawn == null)
            return;

        pawn.SetMoveType(MoveType.Walk);
        pawn.RenderColor = new Color32(255, 255, 255, 255);
        CancelFreezeTimer(gameClient.Slot);
    }

    internal static void CancelFreezeTimer(int slot)
    {
        if (FreezeTimers.TryGetValue(slot, out var timerId))
        {
            _sharedSystem?.GetModSharp().StopTimer(timerId);
            FreezeTimers.Remove(slot);
        }
    }
}
