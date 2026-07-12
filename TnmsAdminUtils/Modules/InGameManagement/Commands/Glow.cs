using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using TnmsAdminUtils.Utils;
using TnmsPluginFoundation.Extensions.Client;
using TnmsPluginFoundation.Models.Command;
using TnmsPluginFoundation.Models.Command.Validators;
using TnmsPluginFoundation.Models.Command.Validators.RangedValidators;
using TnmsPluginFoundation.Utils.Entity;

namespace TnmsAdminUtils.Modules.InGameManagement.Commands;

public class Glow(IServiceProvider provider) : TnmsAbstractCommandBase(provider)
{
    public override string CommandName => "glow";
    public override string CommandDescription => "Toggles outline glow on a player.";

    public override TnmsCommandRegistrationType CommandRegistrationType =>
        TnmsCommandRegistrationType.Client | TnmsCommandRegistrationType.Server;

    protected override ICommandValidator? GetValidator() => new CompositeValidator()
        .Add(new PermissionValidator("tnms.adminutil.management.ingame.command.glow", true))
        .Add(new ArgumentCountValidator(2, true))
        .Add(new TargetValidator(1, true))
        .Add(new RangedArgumentValidator<int>(0, 1, 2, true));

    protected override ValidationFailureResult OnValidationFailed(ValidationFailureContext context)
    {
        switch (context.Validator)
        {
            case ArgumentCountValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Glow.Notification.Usage"));
                break;
            case PermissionValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Common.ValidationFailure.NotEnoughPermissions"));
                break;
            case TargetValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Common.ValidationFailure.NoValidTargetsFound"));
                break;
            case IRangedArgumentValidator rangedArgumentValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Common.ValidationFailure.ArgumentIsMustBeInRange", rangedArgumentValidator.ArgumentIndex, rangedArgumentValidator.GetRangeDescription()));
                break;
        }

        return ValidationFailureResult.SilentAbort();
    }

    private static readonly Dictionary<string, Color32> KnownColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["red"]     = new Color32(255, 0, 0, 255),
        ["green"]   = new Color32(0, 255, 0, 255),
        ["blue"]    = new Color32(0, 0, 255, 255),
        ["yellow"]  = new Color32(255, 255, 0, 255),
        ["orange"]  = new Color32(255, 165, 0, 255),
        ["purple"]  = new Color32(128, 0, 128, 255),
        ["cyan"]    = new Color32(0, 255, 255, 255),
        ["pink"]    = new Color32(255, 192, 203, 255),
        ["white"]   = new Color32(255, 255, 255, 255),
    };

    protected override void ExecuteCommand(IGameClient? client, StringCommand commandInfo, ValidatedArguments? validatedArguments)
    {
        var targets = validatedArguments!.GetArgument<List<IGameClient>>(1)!;
        int value = validatedArguments.GetArgument<int>(2);
        bool enable = value == 1;

        Color32 glowColor = new(255, 255, 255, 255);
        string colorName = "white";
        if (enable && commandInfo.ArgCount > 3)
        {
            colorName = commandInfo.GetArg(3);
            if (KnownColors.TryGetValue(colorName, out var parsed))
                glowColor = parsed;
        }

        foreach (var gameClient in targets)
        {
            var pawn = gameClient.GetPlayerPawn();
            if (pawn == null)
                continue;

            var glowProp = pawn.GetGlowProperty();
            glowProp.Glowing = enable;
            if (enable)
                glowProp.GlowColorOverride = glowColor;
        }

        string executor = PlayerUtil.GetPlayerName(client);
        string targetName = targets.GetTargetName();
        string state = enable ? $"ON ({colorName})" : "OFF";
        Plugin.TnmsLogger.LogAdminAction(client, $"Admin {executor} set glow {state} on {targetName}");

        foreach (var gameClient in SharedSystem.GetModSharp().GetIServer().GetGameClients())
        {
            if (gameClient.IsFakeClient || gameClient.IsHltv)
                continue;

            gameClient.GetPlayerController()?
                .PrintToChat(
                    ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(gameClient, "Glow.Broadcast.GlowSet", executor, targetName, state));
        }
    }
}
