using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using TnmsAdminUtils.Utils;
using TnmsPluginFoundation.Extensions.Client;
using TnmsPluginFoundation.Models.Command;
using TnmsPluginFoundation.Models.Command.Validators;
using TnmsPluginFoundation.Models.Command.Validators.RangedValidators;
using TnmsPluginFoundation.Utils.Entity;

namespace TnmsAdminUtils.Modules.InGameManagement.Commands;

public class Slap(IServiceProvider provider): TnmsAbstractCommandBase(provider)
{
    public override string CommandName => "slap";
    public override string CommandDescription => "Slaps a specified player.";

    public override TnmsCommandRegistrationType CommandRegistrationType =>
        TnmsCommandRegistrationType.Client | TnmsCommandRegistrationType.Server;

    protected override ICommandValidator? GetValidator() => new CompositeValidator()
        .Add(new PermissionValidator("tnms.adminutil.management.ingame.command.slap", true))
        .Add(new ArgumentCountValidator(1, true))
        .Add(new TargetValidator(1, true))
        .Add(new RangedArgumentValidator<int>(0, int.MaxValue, 2, 0, true));

    protected override ValidationFailureResult OnValidationFailed(ValidationFailureContext context)
    {
        switch (context.Validator)
        {
            case ArgumentCountValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Slay.Notification.Usage"));
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
        int damage = validatedArguments.GetArgument<int>(2);

        foreach (var gameClient in targets)
        {
            var pawn = gameClient?.GetPlayerController()?.GetPlayerPawn();
            
            if (pawn == null)
                continue;
            
            if (damage > 0)
            {
                int newHp = Math.Max(0, pawn.Health - damage);

                if (newHp > 0)
                {
                    pawn.Health = newHp;
                }
                else
                {
                    pawn.Slay();
                }
            }
            
            var rnd = Random.Shared;
            float dx = (rnd.Next(180) + 50) * (rnd.Next(2) == 1 ? -1 : 1);
            float dy = (rnd.Next(180) + 50) * (rnd.Next(2) == 1 ? -1 : 1);
            float dz = rnd.Next(200) + 100;
            pawn.ApplyAbsVelocityImpulse(new Vector(dx, dy, dz));
            
            // TODO Play slap sound, and sound can be specified in config later
        }

        string executor = PlayerUtil.GetPlayerName(client);
        string targetName = targets.GetTargetName();

        
        if (damage > 0)
        {
            Plugin.TnmsLogger.LogAdminAction(client, $"Admin {executor} has been slapped {targetName} with damage {damage}");
            foreach (var gameClient in SharedSystem.GetModSharp().GetIServer().GetGameClients())
            {
                if (gameClient.IsFakeClient || gameClient.IsHltv)
                    continue;
            
                gameClient.GetPlayerController()?
                    .PrintToChat(
                        ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(gameClient, "Slap.Broadcast.Slapped.WithDamage", executor, targets.GetTargetName(), damage)
                    );
            }
        }
        else
        {
            Plugin.TnmsLogger.LogAdminAction(client, $"Admin {executor} has been slapped {targetName}");
            foreach (var gameClient in SharedSystem.GetModSharp().GetIServer().GetGameClients())
            {
                if (gameClient.IsFakeClient || gameClient.IsHltv)
                    continue;
            
                gameClient.GetPlayerController()?
                    .PrintToChat(
                        ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(gameClient, "Slap.Broadcast.Slapped.NoDamage", executor, targets.GetTargetName())
                    );
            }
        }
    }
}