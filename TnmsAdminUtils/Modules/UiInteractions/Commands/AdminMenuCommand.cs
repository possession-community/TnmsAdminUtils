using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using TnmsPluginFoundation.Models.Command;
using TnmsPluginFoundation.Models.Command.Validators;

namespace TnmsAdminUtils.Modules.UiInteractions.Commands;

public class AdminMenuCommand(IServiceProvider provider) : TnmsAbstractCommandBase(provider)
{
    public override string CommandName => "admin";
    public override string CommandDescription => "Opens the admin menu.";

    public override TnmsCommandRegistrationType CommandRegistrationType => TnmsCommandRegistrationType.Client;

    protected override ICommandValidator? GetValidator() => new PermissionValidator("tnms.adminutil.menu", true);

    protected override ValidationFailureResult OnValidationFailed(ValidationFailureContext context)
    {
        if (context.Validator is PermissionValidator)
            PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Common.ValidationFailure.NotEnoughPermissions"));

        return ValidationFailureResult.SilentAbort();
    }

    protected override void ExecuteCommand(IGameClient? client, StringCommand commandInfo, ValidatedArguments? validatedArguments)
    {
        if (client == null)
            return;

        ((TnmsAdminUtils)Plugin).AdminMenu.Open(client);
    }
}
