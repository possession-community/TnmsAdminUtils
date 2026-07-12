using Sharp.Shared.Enums;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using TnmsAdminUtils.Utils;
using TnmsPluginFoundation.Extensions.Client;
using TnmsPluginFoundation.Models.Command;
using TnmsPluginFoundation.Models.Command.Validators;
using TnmsPluginFoundation.Utils.Entity;

namespace TnmsAdminUtils.Modules.InGameManagement.Commands;

public class Color(IServiceProvider provider) : TnmsAbstractCommandBase(provider)
{
    public override string CommandName => "color";
    public override string CommandDescription => "Sets a player's render color.";

    public override TnmsCommandRegistrationType CommandRegistrationType =>
        TnmsCommandRegistrationType.Client | TnmsCommandRegistrationType.Server;

    protected override ICommandValidator? GetValidator() => new CompositeValidator()
        .Add(new PermissionValidator("tnms.adminutil.management.ingame.command.color", true))
        .Add(new ArgumentCountValidator(1, true))
        .Add(new TargetValidator(1, true));

    protected override ValidationFailureResult OnValidationFailed(ValidationFailureContext context)
    {
        switch (context.Validator)
        {
            case ArgumentCountValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Color.Notification.Usage"));
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
        ["black"]   = new Color32(0, 0, 0, 255),
    };

    protected override void ExecuteCommand(IGameClient? client, StringCommand commandInfo, ValidatedArguments? validatedArguments)
    {
        var targets = validatedArguments!.GetArgument<List<IGameClient>>(1)!;

        Color32 color = new(255, 255, 255, 255);
        string colorName = "white";

        if (commandInfo.ArgCount > 2)
        {
            colorName = commandInfo.GetArg(2);
            if (!KnownColors.TryGetValue(colorName, out color))
            {
                PrintMessageToServerOrPlayerChat(client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(client, "Color.Notification.InvalidColor"));
                return;
            }
        }

        foreach (var gameClient in targets)
        {
            var pawn = gameClient.GetPlayerPawn();
            if (pawn == null)
                continue;

            pawn.RenderMode = RenderMode.TransAlpha;
            pawn.RenderColor = color;
        }

        string executor = PlayerUtil.GetPlayerName(client);
        string targetName = targets.GetTargetName();
        Plugin.TnmsLogger.LogAdminAction(client, $"Admin {executor} set {targetName} color to {colorName}");

        foreach (var gameClient in SharedSystem.GetModSharp().GetIServer().GetGameClients())
        {
            if (gameClient.IsFakeClient || gameClient.IsHltv)
                continue;

            gameClient.GetPlayerController()?
                .PrintToChat(
                    ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(gameClient, "Color.Broadcast.ColorSet", executor, targetName, colorName));
        }
    }
}
