using Sharp.Shared.GameEntities;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using TnmsAdminUtils.Utils;
using TnmsPluginFoundation.Extensions.Client;
using TnmsPluginFoundation.Models.Command;
using TnmsPluginFoundation.Models.Command.Validators;
using TnmsPluginFoundation.Models.Command.Validators.RangedValidators;
using TnmsPluginFoundation.Utils.Entity;

namespace TnmsAdminUtils.Modules.InGameManagement.Commands;

public class Drop(IServiceProvider provider) : TnmsAbstractCommandBase(provider)
{
    public override string CommandName => "drop";
    public override string CommandDescription => "Forces a player to drop a weapon by index.";

    public override TnmsCommandRegistrationType CommandRegistrationType =>
        TnmsCommandRegistrationType.Client | TnmsCommandRegistrationType.Server;

    protected override ICommandValidator? GetValidator() => new CompositeValidator()
        .Add(new PermissionValidator("tnms.adminutil.management.ingame.command.give", true))
        .Add(new ArgumentCountValidator(2, true))
        .Add(new TargetValidator(1, true))
        .Add(new RangedArgumentValidator<int>(0, 64, 2, true));

    protected override ValidationFailureResult OnValidationFailed(ValidationFailureContext context)
    {
        switch (context.Validator)
        {
            case ArgumentCountValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Drop.Notification.Usage"));
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
        int weaponIndex = validatedArguments.GetArgument<int>(2);
        var entityManager = SharedSystem.GetEntityManager();

        foreach (var gameClient in targets)
        {
            var pawn = gameClient.GetPlayerPawn();
            if (pawn == null)
                continue;

            var weaponService = pawn.GetWeaponService();
            if (weaponService == null)
                continue;

            var weapons = weaponService.GetMyWeapons();
            if (weaponIndex >= weapons.Count)
            {
                PrintMessageToServerOrPlayerChat(client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(client, "Drop.Notification.InvalidIndex", weaponIndex, weapons.Count));
                return;
            }

            var weapon = entityManager.FindEntityByHandle(weapons[weaponIndex]);
            if (weapon == null)
                continue;

            pawn.DropWeapon(weapon);
        }

        string executor = PlayerUtil.GetPlayerName(client);
        string targetName = targets.GetTargetName();
        Plugin.TnmsLogger.LogAdminAction(client, $"Admin {executor} forced {targetName} to drop weapon at index {weaponIndex}");

        foreach (var gameClient in SharedSystem.GetModSharp().GetIServer().GetGameClients())
        {
            if (gameClient.IsFakeClient || gameClient.IsHltv)
                continue;

            gameClient.GetPlayerController()?
                .PrintToChat(
                    ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(gameClient, "Drop.Broadcast.Dropped", executor, targetName, weaponIndex));
        }
    }
}
