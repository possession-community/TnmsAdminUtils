using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using TnmsPluginFoundation.Extensions.Client;
using TnmsPluginFoundation.Models.Command;
using TnmsPluginFoundation.Models.Command.Validators;
using TnmsPluginFoundation.Models.Command.Validators.RangedValidators;
using TnmsPluginFoundation.Utils.Entity;
using TnmsAdminUtils.Modules.UiInteractions;

namespace TnmsAdminUtils.Modules.InGameManagement.Commands.GameRules;

public class AddTime(IServiceProvider provider) : TnmsAbstractCommandBase(provider)
{
    public override string CommandName => "addtime";
    public override string CommandDescription => "Adds time to the current round timer.";

    private const string Permission = "tnms.adminutil.management.ingame.command.addtime";

    public override TnmsCommandRegistrationType CommandRegistrationType =>
        TnmsCommandRegistrationType.Client | TnmsCommandRegistrationType.Server;

    protected override void OnRegistered()
        => ((TnmsAdminUtils)Plugin).AdminMenu.Registry.Register(AdminMenuEntry.Create(CommandName, Permission).Usage("AddTime.Notification.Usage").Preset("addtime", "AdminMenu.Step.Seconds"));

    protected override ICommandValidator? GetValidator() => new CompositeValidator()
        .Add(new PermissionValidator(Permission, true))
        .Add(new ArgumentCountValidator(1, true))
        .Add(new RangedArgumentValidator<int>(int.MinValue, int.MaxValue, 1, true));

    protected override ValidationFailureResult OnValidationFailed(ValidationFailureContext context)
    {
        switch (context.Validator)
        {
            case ArgumentCountValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "AddTime.Notification.Usage"));
                break;
            case PermissionValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Common.ValidationFailure.NotEnoughPermissions"));
                break;
            case IRangedArgumentValidator rangedArgumentValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Common.ValidationFailure.ArgumentIsMustBeInRange", rangedArgumentValidator.ArgumentIndex, rangedArgumentValidator.GetRangeDescription()));
                break;
        }
        
        return ValidationFailureResult.SilentAbort();
    }

    protected override void ExecuteCommand(IGameClient? client, StringCommand commandInfo, ValidatedArguments? validatedArguments)
    {
        int extendSeconds = validatedArguments!.GetArgument<int>(1);

        int currentRoundTimeLimit = GameRulesUtil.GetRoundTime();
        
        int newRoundTimeLimit = currentRoundTimeLimit + extendSeconds;
        
        
        string executor = PlayerUtil.GetPlayerName(client);

        if (extendSeconds < 0)
        {
            int diffTime = -extendSeconds;
            if (newRoundTimeLimit < 0)
            {
                diffTime = Math.Abs(-extendSeconds - -newRoundTimeLimit);
                newRoundTimeLimit = 0;
            }
            
            Plugin.TnmsLogger.LogAdminAction(client, $"Admin {executor} shortened current round time by {diffTime} seconds");
        
            foreach (var gameClient in SharedSystem.GetModSharp().GetIServer().GetGameClients(true, true))
            {
                if (gameClient.IsFakeClient || gameClient.IsHltv)
                    continue;
            
                gameClient.GetPlayerController()?
                    .PrintToChat(
                        ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(gameClient, "AddTime.Broadcast.TimeShortened", executor, diffTime));
            }
        }
        else
        {
            Plugin.TnmsLogger.LogAdminAction(client, $"Admin {executor} extended current round time by {extendSeconds} seconds");
        
            foreach (var gameClient in SharedSystem.GetModSharp().GetIServer().GetGameClients(true, true))
            {
                if (gameClient.IsFakeClient || gameClient.IsHltv)
                    continue;
            
                gameClient.GetPlayerController()?
                    .PrintToChat(
                        ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(gameClient, "AddTime.Broadcast.TimeAdded", executor, extendSeconds));
            }
        }
        
        GameRulesUtil.SetRoundTime(newRoundTimeLimit);
    }
}