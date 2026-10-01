using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using TnmsPluginFoundation.Models.Command;
using TnmsPluginFoundation.Models.Command.Validators;

namespace TnmsAdminUtils.Modules.UiInteractions.Commands;

/// <summary>
/// Reads menu.json again and rebuilds the admins' command lists, which are otherwise kept until the next load.
/// </summary>
public class AdminMenuReloadCommand(IServiceProvider provider) : TnmsAbstractCommandBase(provider)
{
    public override string CommandName => "adminmenu_reload";
    public override string CommandDescription => "Reloads menu.json and the admin menu command lists.";

    public override TnmsCommandRegistrationType CommandRegistrationType =>
        TnmsCommandRegistrationType.Client | TnmsCommandRegistrationType.Server;

    protected override ICommandValidator? GetValidator() => new PermissionValidator("tnms.adminutil.menu.reload", true);

    protected override ValidationFailureResult OnValidationFailed(ValidationFailureContext context)
    {
        if (context.Validator is PermissionValidator)
            PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Common.ValidationFailure.NotEnoughPermissions"));

        return ValidationFailureResult.SilentAbort();
    }

    protected override void ExecuteCommand(IGameClient? client, StringCommand commandInfo, ValidatedArguments? validatedArguments)
    {
        ((TnmsAdminUtils)Plugin).AdminMenu.Reload();
        PrintMessageToServerOrPlayerChat(client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(client, "AdminMenu.Reloaded"));
    }
}
