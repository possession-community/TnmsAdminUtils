using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using TnmsAdminUtils.Modules.UiInteractions.Panel;
using TnmsPluginFoundation.Models.Command;
using TnmsPluginFoundation.Models.Command.Validators;

namespace TnmsAdminUtils.Modules.UiInteractions.Commands;

public class AdminPanelCommand(IServiceProvider provider) : TnmsAbstractCommandBase(provider)
{
    public override string CommandName => "adminpanel";
    public override string CommandDescription => "Opens the admin panel.";

    public override TnmsCommandRegistrationType CommandRegistrationType => TnmsCommandRegistrationType.Client;

    protected override ICommandValidator? GetValidator() => new PermissionValidator(AdminPanelService.Permission, true);

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

        ((TnmsAdminUtils)Plugin).AdminMenu.OpenPanel(client);
    }
}
