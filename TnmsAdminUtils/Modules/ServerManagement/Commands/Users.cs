using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using TnmsPluginFoundation.Extensions.Client;
using TnmsPluginFoundation.Models.Command;
using TnmsPluginFoundation.Models.Command.Validators;
using TnmsPluginFoundation.Utils.Entity;
using TnmsAdminUtils.Modules.UiInteractions;

namespace TnmsAdminUtils.Modules.ServerManagement.Commands;

public class Users(IServiceProvider provider) : TnmsAbstractCommandBase(provider)
{
    public override string CommandName => "users";
    public override string CommandDescription => "Lists online players.";

    private const string Permission = "tnms.adminutil.management.server.command.users";

    public override TnmsCommandRegistrationType CommandRegistrationType =>
        TnmsCommandRegistrationType.Client | TnmsCommandRegistrationType.Server;

    protected override void OnRegistered()
        => ((TnmsAdminUtils)Plugin).AdminMenu.Registry.Register(AdminMenuEntry.Create(CommandName, Permission));

    protected override ICommandValidator? GetValidator() => new CompositeValidator()
        .Add(new PermissionValidator(Permission, true));

    protected override ValidationFailureResult OnValidationFailed(ValidationFailureContext context)
    {
        switch (context.Validator)
        {
            case PermissionValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Common.ValidationFailure.NotEnoughPermissions"));
                break;
        }

        return ValidationFailureResult.SilentAbort();
    }

    protected override void ExecuteCommand(IGameClient? client, StringCommand commandInfo, ValidatedArguments? validatedArguments)
    {
        PrintMessageToServerOrPlayerChat(client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(client, "Users.Notification.Header"));

        foreach (var gameClient in SharedSystem.GetModSharp().GetIServer().GetGameClients(true, true))
        {
            if (gameClient.IsFakeClient || gameClient.IsHltv)
                continue;

            string name = gameClient.Name;
            int slot = gameClient.Slot;
            string steamId = gameClient.SteamId.ToString();

            PrintMessageToServerOrPlayerChat(client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(client, "Users.Notification.Entry", slot, name, steamId));
        }
    }
}
