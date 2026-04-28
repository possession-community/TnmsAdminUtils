using System.Globalization;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using TnmsExtendableTargeting.Shared;
using TnmsPluginFoundation.Extensions.Client;
using TnmsPluginFoundation.Models.Command;
using TnmsPluginFoundation.Models.Command.Validators;
using TnmsPluginFoundation.Utils.Entity;

namespace TnmsAdminUtils.Modules.InGameManagement.Commands;

public class Slay(IServiceProvider provider): TnmsAbstractCommandBase(provider)
{
    public override string CommandName => "slay";
    public override string CommandDescription => "Slays a specified player.";

    public override TnmsCommandRegistrationType CommandRegistrationType =>
        TnmsCommandRegistrationType.Client | TnmsCommandRegistrationType.Server;

    protected override ICommandValidator? GetValidator() => new CompositeValidator()
        .Add(new PermissionValidator("tnms.adminutil.management.ingame.command.slay", true))
        .Add(new ArgumentCountValidator(1, true))
        .Add(new ExtendableTargetValidator(1, true));

    protected override ValidationFailureResult OnValidationFailed(ValidationFailureContext context)
    {
        switch (context.Validator)
        {
            case ArgumentCountValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Slap.Notification.Usage"));
                break;
            case PermissionValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Common.ValidationFailure.NotEnoughPermissions"));
                break;
            case ExtendableTargetValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Common.ValidationFailure.NoValidTargetsFound"));
                break;
        }
        
        return ValidationFailureResult.SilentAbort();
    }

    protected override void ExecuteCommand(IGameClient? client, StringCommand commandInfo, ValidatedArguments? validatedArguments)
    {
        var targets = validatedArguments!.GetArgument<ITargetingResult>(1)!;

        foreach (var gameClient in targets.GetTargets())
        {
            gameClient.GetPlayerController()?.GetPlayerPawn()?.Slay();
        }
        
        


        string executor = PlayerUtil.GetPlayerName(client);
        string targetName = targets.GetTargetName(CultureInfo.CurrentCulture);
        Plugin.TnmsLogger.LogAdminAction(client, $"Admin {executor} slayed {targetName}");

        foreach (var gameClient in SharedSystem.GetModSharp().GetIServer().GetGameClients())
        {
            if (gameClient.IsFakeClient || gameClient.IsHltv)
                continue;
            
            gameClient.GetPlayerController()?
                .PrintToChat(
                    ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(gameClient, "Slay.Broadcast.Slayed", executor, targets.GetTargetName(Plugin.Localizer.GetClientCulture(gameClient)))
                );
        }
    }
}