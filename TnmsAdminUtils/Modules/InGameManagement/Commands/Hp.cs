using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using TnmsAdminUtils.Utils;
using TnmsPluginFoundation.Extensions.Client;
using TnmsPluginFoundation.Models.Command;
using TnmsPluginFoundation.Models.Command.Validators;
using TnmsPluginFoundation.Models.Command.Validators.RangedValidators;
using TnmsPluginFoundation.Utils.Entity;
using TnmsAdminUtils.Modules.UiInteractions;

namespace TnmsAdminUtils.Modules.InGameManagement.Commands;

public class Hp(IServiceProvider provider) : TnmsAbstractCommandBase(provider)
{
    public override string CommandName => "hp";
    public override string CommandDescription => "Sets a player's health.";

    private const string Permission = "tnms.adminutil.management.ingame.command.hp";

    public override TnmsCommandRegistrationType CommandRegistrationType =>
        TnmsCommandRegistrationType.Client | TnmsCommandRegistrationType.Server;

    protected override void OnRegistered()
        => ((TnmsAdminUtils)Plugin).AdminMenu.Registry.Register(AdminMenuEntry.Create(CommandName, Permission).Target().Preset("hp"));

    protected override ICommandValidator? GetValidator() => new CompositeValidator()
        .Add(new PermissionValidator(Permission, true))
        .Add(new ArgumentCountValidator(2, true))
        .Add(new TargetValidator(1, true))
        .Add(new RangedArgumentValidator<int>(1, int.MaxValue, 2, true));

    protected override ValidationFailureResult OnValidationFailed(ValidationFailureContext context)
    {
        switch (context.Validator)
        {
            case ArgumentCountValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Hp.Notification.Usage"));
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

    protected override void ExecuteCommand(IGameClient? client, StringCommand commandInfo, ValidatedArguments? validatedArguments)
    {
        var targets = validatedArguments!.GetArgument<List<IGameClient>>(1)!;
        int health = validatedArguments.GetArgument<int>(2);

        foreach (var gameClient in targets)
        {
            PlayerUtil.SetPlayerHealth(gameClient, health);
            if (health > 100)
                PlayerUtil.SetPlayerMaxHealth(gameClient, health);
        }

        string executor = PlayerUtil.GetPlayerName(client);
        string targetName = targets.GetTargetName();
        Plugin.TnmsLogger.LogAdminAction(client, $"Admin {executor} set {targetName} health to {health}");

        foreach (var gameClient in SharedSystem.GetModSharp().GetIServer().GetGameClients(true, true))
        {
            if (gameClient.IsFakeClient || gameClient.IsHltv)
                continue;

            gameClient.GetPlayerController()?
                .PrintToChat(
                    ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(gameClient, "Hp.Broadcast.HealthSet", executor, targetName, health));
        }
    }
}
