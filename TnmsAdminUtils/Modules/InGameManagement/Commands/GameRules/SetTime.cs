using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using TnmsPluginFoundation.Models.Command;
using TnmsPluginFoundation.Models.Command.Validators;
using TnmsPluginFoundation.Models.Command.Validators.RangedValidators;
using TnmsPluginFoundation.Utils.Entity;
using TnmsAdminUtils.Modules.UiInteractions;

namespace TnmsAdminUtils.Modules.InGameManagement.Commands.GameRules;

public class SetTime(IServiceProvider provider) : TnmsAbstractCommandBase(provider)
{
    public override string CommandName => "settime";
    public override string CommandDescription => "Sets time of the current round timer.";

    private const string Permission = "tnms.adminutil.management.ingame.command.settime";

    public override TnmsCommandRegistrationType CommandRegistrationType =>
        TnmsCommandRegistrationType.Client | TnmsCommandRegistrationType.Server;

    protected override void OnRegistered()
        => ((TnmsAdminUtils)Plugin).AdminMenu.Registry.Register(AdminMenuEntry.Create(CommandName, Permission).Preset("settime", "AdminMenu.Step.Seconds"));

    protected override ICommandValidator? GetValidator() => new CompositeValidator()
        .Add(new PermissionValidator(Permission, true))
        .Add(new ArgumentCountValidator(1, true))
        .Add(new RangedArgumentValidator<int>(int.MinValue, int.MaxValue, 1, true));

    protected override ValidationFailureResult OnValidationFailed(ValidationFailureContext context)
    {
        switch (context.Validator)
        {
            case ArgumentCountValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "SetTime.Notification.Usage"));
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
        // TODO() A bit complicated
        // int setSeconds = validatedArguments!.GetArgument<int>(1);
        //
        // Console.WriteLine($"SetTime.Broadcast.TimeSet: {setSeconds}");
        //
        // GameRulesUtil.SetRoundTime(setSeconds);
    }
}