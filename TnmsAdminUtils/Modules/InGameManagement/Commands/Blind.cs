using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using TnmsAdminUtils.Utils;
using TnmsPluginFoundation.Extensions.Client;
using TnmsPluginFoundation.Models.Command;
using TnmsPluginFoundation.Models.Command.Validators;
using TnmsPluginFoundation.Utils.Entity;

namespace TnmsAdminUtils.Modules.InGameManagement.Commands;

public class Blind(IServiceProvider provider) : TnmsAbstractCommandBase(provider)
{
    public override string CommandName => "blind";
    public override string CommandDescription => "Blinds a player.";

    public override TnmsCommandRegistrationType CommandRegistrationType =>
        TnmsCommandRegistrationType.Client | TnmsCommandRegistrationType.Server;

    protected override ICommandValidator? GetValidator() => new CompositeValidator()
        .Add(new PermissionValidator("tnms.adminutil.management.ingame.command.blind", true))
        .Add(new ArgumentCountValidator(1, true))
        .Add(new TargetValidator(1, true));

    protected override ValidationFailureResult OnValidationFailed(ValidationFailureContext context)
    {
        switch (context.Validator)
        {
            case ArgumentCountValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Blind.Notification.Usage"));
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

        float holdTime = 999f;
        if (commandInfo.ArgCount > 2 && float.TryParse(commandInfo.GetArg(2), out float parsed) && parsed > 0)
            holdTime = parsed;

        var modSharp = SharedSystem.GetModSharp();

        foreach (var gameClient in targets)
        {
            // FADE_IN (0x0001) | PURGE (0x0010) = 0x0011
            uint color = 0u | (0u << 8) | (0u << 16) | (255u << 24); // Black, full alpha
            var fadeMsg = new CUserMessageFade
            {
                Duration = (uint)(0.2f * 512),
                HoldTime = (uint)(holdTime * 512),
                Flags = 0x0011,
                Color = color
            };
            modSharp.SendNetMessage(new RecipientFilter(gameClient), fadeMsg);
        }

        string executor = PlayerUtil.GetPlayerName(client);
        string targetName = targets.GetTargetName();
        Plugin.TnmsLogger.LogAdminAction(client, $"Admin {executor} blinded {targetName}");

        foreach (var gameClient in SharedSystem.GetModSharp().GetIServer().GetGameClients())
        {
            if (gameClient.IsFakeClient || gameClient.IsHltv)
                continue;

            gameClient.GetPlayerController()?
                .PrintToChat(
                    ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(gameClient, "Blind.Broadcast.Blinded", executor, targetName));
        }
    }
}
